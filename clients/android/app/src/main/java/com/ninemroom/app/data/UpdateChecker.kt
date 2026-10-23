package com.ninemroom.app.data

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.json.JSONObject
import java.net.HttpURLConnection
import java.net.URL

data class UpdateInfo(
    val latestVersion: String,
    val releaseUrl: String,
)

object UpdateChecker {
    private const val MANIFEST_URL =
        "https://raw.githubusercontent.com/Miyu3710/9mRoom/main/update-android.json"
    private const val ATOM_URL = "https://github.com/Miyu3710/9mRoom/releases.atom"

    suspend fun check(currentVersion: String): UpdateInfo? = withContext(Dispatchers.IO) {
        fetchManifest()?.let { manifest ->
            return@withContext manifest.takeIf { compareVersions(it.latestVersion, currentVersion) > 0 }
        }
        fetchAtom(currentVersion)
    }

    private fun fetchManifest(): UpdateInfo? {
        return try {
            val body = readUrl(MANIFEST_URL) ?: return null
            val root = JSONObject(body)
            val version = root.optString("version").trim()
            val releaseUrl = root.optString("release").trim()
            if (version.isBlank() || releaseUrl.isBlank()) null
            else UpdateInfo(version.removePrefix("v"), releaseUrl)
        } catch (_: Exception) {
            null
        }
    }

    private fun fetchAtom(currentVersion: String): UpdateInfo? {
        val body = readUrl(ATOM_URL) ?: return null
        val entries = Regex("<entry>(.*?)</entry>", RegexOption.DOT_MATCHES_ALL)
            .findAll(body)
            .map { it.groupValues[1] }

        for (entry in entries) {
            val releaseUrl = Regex("href=\"([^\"]+/releases/tag/[^\"]+)\"")
                .find(entry)?.groupValues?.getOrNull(1).orEmpty()
            val tag = releaseUrl.substringAfterLast('/')
            if (!tag.startsWith("android-v", ignoreCase = true)) {
                continue
            }

            val latest = tag.removePrefix("android-v").removePrefix("v").trim()
            if (latest.isBlank() || releaseUrl.isBlank()) {
                continue
            }

            return if (compareVersions(latest, currentVersion) > 0) {
                UpdateInfo(latest, releaseUrl)
            } else {
                null
            }
        }

        return null
    }

    private fun readUrl(url: String): String? {
        val connection = (URL(url).openConnection() as HttpURLConnection).apply {
            connectTimeout = 10_000
            readTimeout = 10_000
            requestMethod = "GET"
            setRequestProperty("User-Agent", "9mRoom-Android")
        }

        return try {
            if (connection.responseCode !in 200..299) {
                null
            } else {
                connection.inputStream.bufferedReader().use { it.readText() }
            }
        } finally {
            connection.disconnect()
        }
    }

    private fun compareVersions(left: String, right: String): Int {
        val a = left.trim().removePrefix("v").split('.', '-', '+')
        val b = right.trim().removePrefix("v").split('.', '-', '+')
        val length = maxOf(a.size, b.size)
        for (index in 0 until length) {
            val x = a.getOrNull(index)?.toIntOrNull() ?: 0
            val y = b.getOrNull(index)?.toIntOrNull() ?: 0
            if (x != y) return x.compareTo(y)
        }
        return 0
    }
}
