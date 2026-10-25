package com.ninemroom.app.ui

import android.content.Context
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.graphics.Matrix
import android.media.ExifInterface
import android.net.Uri
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.gestures.detectTransformGestures
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.safeDrawingPadding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Close
import androidx.compose.material3.Button
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.graphics.drawscope.DrawScope
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.layout.onSizeChanged
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.IntSize
import androidx.compose.ui.unit.dp
import androidx.compose.ui.window.Dialog
import androidx.compose.ui.window.DialogProperties
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import kotlin.math.max
import kotlin.math.min
import kotlin.math.roundToInt

private class AvatarCropState {
    var viewport by mutableStateOf(IntSize.Zero)
    var zoom by mutableFloatStateOf(1f)
    var offset by mutableStateOf(Offset.Zero)
}

@Composable
fun AvatarCropDialog(
    uri: Uri,
    onDismiss: () -> Unit,
    onConfirm: (Bitmap) -> Unit,
) {
    val context = LocalContext.current
    var bitmap by remember(uri) { mutableStateOf<Bitmap?>(null) }
    var loading by remember(uri) { mutableStateOf(true) }
    var loadError by remember(uri) { mutableStateOf<String?>(null) }
    val cropState = remember(bitmap) { AvatarCropState() }

    LaunchedEffect(uri) {
        loading = true
        loadError = null
        bitmap = withContext(Dispatchers.IO) { decodeBitmap(context, uri) }
        loading = false
        if (bitmap == null) {
            loadError = "无法读取这张图片，请换一张"
        }
    }

    Dialog(
        onDismissRequest = onDismiss,
        properties = DialogProperties(
            usePlatformDefaultWidth = false,
            dismissOnClickOutside = false,
        ),
    ) {
        Surface(color = Color.Black, modifier = Modifier.fillMaxSize()) {
            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .safeDrawingPadding(),
            ) {
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(56.dp)
                        .padding(horizontal = 8.dp),
                    verticalAlignment = Alignment.CenterVertically,
                ) {
                    Text(
                        "裁剪头像",
                        style = MaterialTheme.typography.titleLarge,
                        color = Color.White,
                        modifier = Modifier
                            .weight(1f)
                            .padding(start = 12.dp),
                    )
                    IconButton(onClick = onDismiss) {
                        Icon(
                            Icons.Outlined.Close,
                            contentDescription = "取消",
                            tint = Color.White,
                        )
                    }
                }

                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .weight(1f)
                        .padding(horizontal = 16.dp, vertical = 8.dp),
                    contentAlignment = Alignment.Center,
                ) {
                    when {
                        loading -> CircularProgressIndicator(color = Color.White)
                        bitmap == null -> Text(
                            loadError ?: "无法读取图片",
                            color = Color.White,
                            style = MaterialTheme.typography.bodyMedium,
                        )
                        else -> CropViewport(
                            bitmap = bitmap!!,
                            state = cropState,
                        )
                    }
                }

                Text(
                    "双指缩放 · 单指拖动调整位置",
                    style = MaterialTheme.typography.labelMedium,
                    color = Color.White.copy(alpha = 0.7f),
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(horizontal = 20.dp),
                )

                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(horizontal = 20.dp, vertical = 16.dp),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically,
                ) {
                    TextButton(onClick = onDismiss) {
                        Text("取消")
                    }
                    val current = bitmap
                    Button(
                        enabled = current != null,
                        onClick = {
                            if (current != null) {
                                cropAvatar(current, cropState)?.let(onConfirm)
                            }
                        },
                    ) {
                        Text("使用头像")
                    }
                }
            }
        }
    }
}

@Composable
private fun CropViewport(
    bitmap: Bitmap,
    state: AvatarCropState,
) {
    val imageBitmap = remember(bitmap) { bitmap.asImageBitmap() }
    BoxWithConstraints(
        modifier = Modifier.fillMaxWidth(),
        contentAlignment = Alignment.Center,
    ) {
        val side = minOf(maxWidth, maxHeight)
        val baseScale = baseScale(bitmap, state.viewport)

        fun clampOffset(candidate: Offset, scale: Float): Offset {
            val maxX = ((bitmap.width * scale - state.viewport.width) / 2f)
                .coerceAtLeast(0f)
            val maxY = ((bitmap.height * scale - state.viewport.height) / 2f)
                .coerceAtLeast(0f)
            return Offset(
                candidate.x.coerceIn(-maxX, maxX),
                candidate.y.coerceIn(-maxY, maxY),
            )
        }

        Box(
            modifier = Modifier
                .size(side)
                .clip(CircleShape)
                .background(Color.Black)
                .onSizeChanged { state.viewport = it }
                .pointerInput(bitmap, state.viewport) {
                    detectTransformGestures { centroid, pan, gestureZoom, _ ->
                        if (state.viewport.width <= 0 || state.viewport.height <= 0) {
                            return@detectTransformGestures
                        }
                        val oldScale = baseScale * state.zoom
                        val newZoom = (state.zoom * gestureZoom).coerceIn(1f, 4f)
                        val newScale = baseScale * newZoom
                        val center = Offset(
                            state.viewport.width / 2f,
                            state.viewport.height / 2f,
                        )
                        val focal = centroid - center
                        val candidate = state.offset + pan +
                            focal * (1f - newScale / oldScale)
                        state.zoom = newZoom
                        state.offset = clampOffset(candidate, newScale)
                    }
                },
        ) {
            Canvas(Modifier.fillMaxSize()) {
                if (state.viewport.width <= 0 || state.viewport.height <= 0) return@Canvas
                val scale = baseScale * state.zoom
                val drawWidth = (bitmap.width * scale).roundToInt()
                val drawHeight = (bitmap.height * scale).roundToInt()
                val left = (size.width / 2f + state.offset.x - drawWidth / 2f).roundToInt()
                val top = (size.height / 2f + state.offset.y - drawHeight / 2f).roundToInt()
                drawImage(
                    image = imageBitmap,
                    srcOffset = IntOffset.Zero,
                    srcSize = IntSize(bitmap.width, bitmap.height),
                    dstOffset = IntOffset(left, top),
                    dstSize = IntSize(drawWidth, drawHeight),
                )
                drawCircle(
                    color = Color.White.copy(alpha = 0.9f),
                    radius = size.minDimension / 2f - 1.dp.toPx(),
                    style = Stroke(width = 2.dp.toPx()),
                )
                drawGrid(size)
            }
        }
    }
}

private fun DrawScope.drawGrid(size: Size) {
    val third = size.width / 3f
    val color = Color.White.copy(alpha = 0.25f)
    for (i in 1..2) {
        drawLine(color, Offset(third * i, 0f), Offset(third * i, size.height), 1f)
        drawLine(color, Offset(0f, third * i), Offset(size.width, third * i), 1f)
    }
}

private fun baseScale(bitmap: Bitmap, viewport: IntSize): Float {
    if (viewport.width <= 0 || viewport.height <= 0) return 1f
    return max(
        viewport.width.toFloat() / bitmap.width,
        viewport.height.toFloat() / bitmap.height,
    )
}

private fun cropAvatar(bitmap: Bitmap, state: AvatarCropState): Bitmap? {
    val viewport = state.viewport
    val scale = baseScale(bitmap, viewport) * state.zoom
    if (viewport.width <= 0 || viewport.height <= 0 || scale <= 0f) return null

    val sidePx = (min(viewport.width, viewport.height) / scale)
        .roundToInt()
        .coerceAtLeast(1)
    val safeSide = sidePx.coerceAtMost(min(bitmap.width, bitmap.height))
    val sourceLeft = ((bitmap.width - safeSide) / 2f - state.offset.x / scale)
        .roundToInt()
        .coerceIn(0, bitmap.width - safeSide)
    val sourceTop = ((bitmap.height - safeSide) / 2f - state.offset.y / scale)
        .roundToInt()
        .coerceIn(0, bitmap.height - safeSide)
    return runCatching {
        Bitmap.createBitmap(bitmap, sourceLeft, sourceTop, safeSide, safeSide)
    }.getOrNull()
}

private fun decodeBitmap(context: Context, uri: Uri): Bitmap? {
    return try {
        val resolver = context.contentResolver
        val bounds = BitmapFactory.Options().apply { inJustDecodeBounds = true }
        resolver.openInputStream(uri)?.use {
            BitmapFactory.decodeStream(it, null, bounds)
        }
        if (bounds.outWidth <= 0 || bounds.outHeight <= 0) return null

        val maxDimension = 2048
        var sample = 1
        while (max(bounds.outWidth, bounds.outHeight) / sample > maxDimension) {
            sample *= 2
        }
        val source = resolver.openInputStream(uri)?.use {
            BitmapFactory.decodeStream(
                it,
                null,
                BitmapFactory.Options().apply { inSampleSize = sample },
            )
        } ?: return null

        val orientation = runCatching {
            resolver.openInputStream(uri)?.use { stream ->
                ExifInterface(stream).getAttributeInt(
                    ExifInterface.TAG_ORIENTATION,
                    ExifInterface.ORIENTATION_NORMAL,
                )
            }
        }.getOrNull() ?: ExifInterface.ORIENTATION_NORMAL

        val matrix = Matrix()
        when (orientation) {
            ExifInterface.ORIENTATION_FLIP_HORIZONTAL -> matrix.postScale(-1f, 1f)
            ExifInterface.ORIENTATION_ROTATE_180 -> matrix.postRotate(180f)
            ExifInterface.ORIENTATION_FLIP_VERTICAL -> matrix.postScale(1f, -1f)
            ExifInterface.ORIENTATION_TRANSPOSE -> {
                matrix.postRotate(90f)
                matrix.postScale(-1f, 1f)
            }
            ExifInterface.ORIENTATION_ROTATE_90 -> matrix.postRotate(90f)
            ExifInterface.ORIENTATION_TRANSVERSE -> {
                matrix.postRotate(270f)
                matrix.postScale(-1f, 1f)
            }
            ExifInterface.ORIENTATION_ROTATE_270 -> matrix.postRotate(270f)
        }
        if (matrix.isIdentity) {
            source
        } else {
            val oriented = Bitmap.createBitmap(
                source,
                0,
                0,
                source.width,
                source.height,
                matrix,
                true,
            )
            if (oriented !== source) source.recycle()
            oriented
        }
    } catch (_: Exception) {
        null
    }
}
