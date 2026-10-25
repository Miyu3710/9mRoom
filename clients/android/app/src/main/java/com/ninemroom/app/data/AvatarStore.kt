package com.ninemroom.app.data

import android.content.Context
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint
import android.net.Uri
import android.util.Base64
import java.io.ByteArrayOutputStream
import java.io.File
import java.security.MessageDigest

object AvatarStore {
    private const val MaxBytes = 96 * 1024
    private const val OutputSize = 512

    private fun cacheFile(context: Context): File =
        File(context.filesDir, "avatar/avatar.jpg")

    fun load(context: Context): AvatarData? {
        return try {
            val file = cacheFile(context)
            if (!file.exists()) return null
            val bytes = file.readBytes()
            if (bytes.isEmpty() || bytes.size > 128 * 1024) return null
            val options = BitmapFactory.Options().apply { inJustDecodeBounds = true }
            BitmapFactory.decodeByteArray(bytes, 0, bytes.size, options)
            AvatarData(
                mime = "image/jpeg",
                hash = sha256(bytes),
                width = options.outWidth.coerceAtLeast(0),
                height = options.outHeight.coerceAtLeast(0),
                data = Base64.encodeToString(bytes, Base64.NO_WRAP),
            )
        } catch (_: Exception) {
            null
        }
    }

    fun save(context: Context, avatar: AvatarData): Boolean {
        return try {
            val file = cacheFile(context)
            file.parentFile?.mkdirs()
            val temp = File(file.parentFile, "avatar.jpg.tmp")
            temp.writeBytes(Base64.decode(avatar.data, Base64.NO_WRAP))
            if (!temp.renameTo(file)) {
                temp.copyTo(file, overwrite = true)
                temp.delete()
            }
            true
        } catch (_: Exception) {
            false
        }
    }

    fun create(context: Context, uri: Uri): AvatarData? {
        return try {
            val bounds = BitmapFactory.Options().apply { inJustDecodeBounds = true }
            context.contentResolver.openInputStream(uri)?.use {
                BitmapFactory.decodeStream(it, null, bounds)
            }
            if (bounds.outWidth <= 0 || bounds.outHeight <= 0) return null

            val sample = maxOf(1, minOf(bounds.outWidth, bounds.outHeight) / OutputSize)
            val decode = BitmapFactory.Options().apply { inSampleSize = sample }
            val source = context.contentResolver.openInputStream(uri)?.use {
                BitmapFactory.decodeStream(it, null, decode)
            } ?: return null

            val side = minOf(source.width, source.height)
            val left = (source.width - side) / 2
            val top = (source.height - side) / 2
            val cropped = Bitmap.createBitmap(source, left, top, side, side)
            if (cropped !== source) source.recycle()

            encode(cropped)
        } catch (_: Exception) {
            null
        }
    }

    fun create(bitmap: Bitmap): AvatarData? {
        return try {
            val side = minOf(bitmap.width, bitmap.height)
            val left = (bitmap.width - side) / 2
            val top = (bitmap.height - side) / 2
            val cropped = Bitmap.createBitmap(bitmap, left, top, side, side)
            encode(cropped)
        } catch (_: Exception) {
            null
        }
    }

    private fun encode(cropped: Bitmap): AvatarData? {
        return try {
            for (size in intArrayOf(OutputSize, 448, 384, 320)) {
                val scaled = if (size == cropped.width) {
                    cropped
                } else {
                    Bitmap.createScaledBitmap(cropped, size, size, true)
                }
                val flattened = Bitmap.createBitmap(size, size, Bitmap.Config.ARGB_8888)
                Canvas(flattened).apply {
                    drawColor(Color.rgb(28, 28, 32))
                    drawBitmap(scaled, 0f, 0f, Paint(Paint.ANTI_ALIAS_FLAG))
                }
                if (scaled !== cropped) scaled.recycle()

                for (quality in intArrayOf(86, 78, 70, 60, 50)) {
                    val output = ByteArrayOutputStream()
                    flattened.compress(Bitmap.CompressFormat.JPEG, quality, output)
                    val bytes = output.toByteArray()
                    if (bytes.size <= MaxBytes) {
                        flattened.recycle()
                        cropped.recycle()
                        return AvatarData(
                            mime = "image/jpeg",
                            hash = sha256(bytes),
                            width = size,
                            height = size,
                            data = Base64.encodeToString(bytes, Base64.NO_WRAP),
                        )
                    }
                }
                flattened.recycle()
            }

            cropped.recycle()
            null
        } catch (_: Exception) {
            cropped.recycle()
            null
        }
    }

    fun bitmap(avatar: AvatarData?, maxSize: Int = 160): Bitmap? {
        if (avatar == null || avatar.data.isBlank()) return null
        return try {
            val bytes = Base64.decode(avatar.data, Base64.NO_WRAP)
            val bounds = BitmapFactory.Options().apply { inJustDecodeBounds = true }
            BitmapFactory.decodeByteArray(bytes, 0, bytes.size, bounds)
            var sample = 1
            while (bounds.outWidth / sample > maxSize * 2 || bounds.outHeight / sample > maxSize * 2) {
                sample *= 2
            }
            BitmapFactory.decodeByteArray(
                bytes,
                0,
                bytes.size,
                BitmapFactory.Options().apply { inSampleSize = sample },
            )
        } catch (_: Exception) {
            null
        }
    }

    private fun sha256(bytes: ByteArray): String =
        MessageDigest.getInstance("SHA-256")
            .digest(bytes)
            .joinToString("") { (it.toInt() and 0xFF).toString(16).padStart(2, '0') }
}
