package com.ninemroom.app.ui

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.ui.unit.dp

@Composable
fun UpdateAvailableDialog(
    currentVersion: String,
    latestVersion: String,
    onUpdate: () -> Unit,
    onCancel: () -> Unit,
    onNever: () -> Unit,
) {
    AlertDialog(
        onDismissRequest = onCancel,
        title = { Text("发现新版本") },
        text = {
            Column {
                Text(
                    "检测到 9mRoom 最新版本 $latestVersion。",
                    style = MaterialTheme.typography.bodyLarge,
                )
                Text(
                    "当前版本：$currentVersion",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        },
        confirmButton = {
            TextButton(onClick = onUpdate) {
                Text("去更新")
            }
        },
        dismissButton = {
            Row(horizontalArrangement = Arrangement.spacedBy(4.dp)) {
                TextButton(onClick = onNever) {
                    Text("不再显示")
                }
                TextButton(onClick = onCancel) {
                    Text("取消")
                }
            }
        },
    )
}
