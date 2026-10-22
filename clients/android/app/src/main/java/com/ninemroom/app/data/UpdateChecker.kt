package com.ninemroom.app.data

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import java.net.HttpURLConnection
import java.net.URL

data class UpdateInfo(
    val latestVersion: String,
    val releaseUrl: String,
)

object UpdateChecker {
    private const val ATOM_URL = "https://github.com/Miyu3710/9mRoom/releases.atom"

    suspend fun check(currentVersion: String): UpdateInfo? = withContext(Dispatchers.IO) {
        val connection = (URL(ATOM_URL).openConnection() as HttpURLConnection).apply {
            connectTimeout = 10_000
            readTimeout = 10_000
            requestMethod = "GET"
            setRequestProperty("User-Agent", "9mRoom-Android/$currentVersion")
        }

        try {
            if (connection.responseCode !in 200..299) {
                throw IllegalStateException("GitHub 返回 ${connection.responseCode}")
            }
            val body = connection.inputStream.bufferedReader().use { it.readText() }
            val firstEntry = body.substringAfter("<entry>", "").substringBefore("</entry>")
            if (firstEntry.isBlank()) return@withContext null
            val title = Regex("<title>(.*?)</title>")
                .find(firstEntry)?.groupValues?.getOrNull(1)?.trim().orEmpty()
            val releaseUrl = Regex("href=\"([^\"]+/releases/tag/[^\"]+)\"")
                .find(firstEntry)?.groupValues?.getOrNull(1).orEmpty()
            val latest = title.split(' ').lastOrNull()?.trim().orEmpty()
            if (latest.isBlank() || releaseUrl.isBlank()) return@withContext null
            if (compareVersions(latest, currentVersion) > 0) {
                UpdateInfo(latest, releaseUrl)
            } else {
                null
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
