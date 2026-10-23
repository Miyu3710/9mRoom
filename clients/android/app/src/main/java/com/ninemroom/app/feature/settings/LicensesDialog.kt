package com.ninemroom.app.feature.settings

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp

private data class LicenseEntry(
    val name: String,
    val version: String,
    val license: String,
    val copyright: String,
)

private val licenses = listOf(
    LicenseEntry(
        name = "Kotlin",
        version = "2.1.20",
        license = "Apache License 2.0",
        copyright = "Copyright JetBrains s.r.o. and Kotlin contributors",
    ),
    LicenseEntry(
        name = "AndroidX Core",
        version = "1.15.0",
        license = "Apache License 2.0",
        copyright = "Copyright The Android Open Source Project",
    ),
    LicenseEntry(
        name = "AndroidX Activity",
        version = "1.10.1",
        license = "Apache License 2.0",
        copyright = "Copyright The Android Open Source Project",
    ),
    LicenseEntry(
        name = "AndroidX Lifecycle",
        version = "2.9.0",
        license = "Apache License 2.0",
        copyright = "Copyright The Android Open Source Project",
    ),
    LicenseEntry(
        name = "Jetpack Compose",
        version = "BOM 2025.04.01",
        license = "Apache License 2.0",
        copyright = "Copyright The Android Open Source Project",
    ),
    LicenseEntry(
        name = "Material 3 / Material Icons",
        version = "Compose BOM",
        license = "Apache License 2.0",
        copyright = "Copyright The Android Open Source Project",
    ),
    LicenseEntry(
        name = "Navigation Compose",
        version = "2.8.9",
        license = "Apache License 2.0",
        copyright = "Copyright The Android Open Source Project",
    ),
    LicenseEntry(
        name = "OkHttp",
        version = "4.12.0",
        license = "Apache License 2.0",
        copyright = "Copyright Square, Inc.",
    ),
    LicenseEntry(
        name = "kotlinx.coroutines",
        version = "transitive",
        license = "Apache License 2.0",
        copyright = "Copyright JetBrains s.r.o. and contributors",
    ),
    LicenseEntry(
        name = "Reorderable",
        version = "3.1.0",
        license = "Apache License 2.0",
        copyright = "Copyright 2023 Calvin Liang",
    ),
)

@Composable
fun LicensesDialog(onDismiss: () -> Unit) {
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("开源许可") },
        text = {
            Column {
                Text(
                    "9mRoom 使用了以下开源项目。完整许可证文本可在 " +
                        "https://www.apache.org/licenses/LICENSE-2.0 查看。",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
                LazyColumn(
                    modifier = Modifier
                        .fillMaxWidth()
                        .heightIn(max = 440.dp)
                        .padding(top = 10.dp),
                    contentPadding = PaddingValues(bottom = 4.dp),
                    verticalArrangement = Arrangement.spacedBy(2.dp),
                ) {
                    items(licenses) { entry ->
                        Column(Modifier.padding(vertical = 8.dp)) {
                            Text(
                                "${entry.name} ${entry.version}",
                                style = MaterialTheme.typography.titleSmall,
                            )
                            Text(
                                entry.license,
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.primary,
                            )
                            Text(
                                entry.copyright,
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                            )
                        }
                        HorizontalDivider(
                            color = MaterialTheme.colorScheme.outlineVariant,
                        )
                    }
                }
            }
        },
        confirmButton = {
            TextButton(onClick = onDismiss) { Text("关闭") }
        },
    )
}
