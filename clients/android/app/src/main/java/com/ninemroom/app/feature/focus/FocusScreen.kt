package com.ninemroom.app.feature.focus

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Pause
import androidx.compose.material.icons.outlined.PlayArrow
import androidx.compose.material.icons.outlined.Refresh
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.FilledTonalButton
import androidx.compose.material3.FilterChip
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.ninemroom.app.NineRoomApplication

@Composable
fun FocusScreen(onTimerChanged: () -> Unit) {
    val context = LocalContext.current
    val focusTimer = (context.applicationContext as NineRoomApplication).focusTimer
    val state by focusTimer.state.collectAsStateWithLifecycle()
    var showDurationDialog by remember { mutableStateOf(false) }

    LazyColumn(
        contentPadding = PaddingValues(20.dp),
        verticalArrangement = Arrangement.spacedBy(14.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        item {
            Column(horizontalAlignment = Alignment.CenterHorizontally) {
                Text("专注", style = MaterialTheme.typography.headlineMedium)
                Text(
                    "把注意力留给眼前这一件事",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.padding(top = 4.dp),
                )
            }
        }

        item {
            TimerRing(
                remainingSeconds = state.remainingSeconds,
                totalSeconds = state.durationSeconds,
                running = state.running,
                onClick = { showDurationDialog = true },
            )
        }

        item {
            Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                Button(
                    onClick = {
                        when {
                            state.running -> focusTimer.pause()
                            state.remainingSeconds in 1 until state.durationSeconds -> focusTimer.resume()
                            else -> focusTimer.start((state.durationSeconds / 60).coerceAtLeast(1))
                        }
                        onTimerChanged()
                    },
                    modifier = Modifier.weight(1f),
                ) {
                    Icon(
                        if (state.running) Icons.Outlined.Pause else Icons.Outlined.PlayArrow,
                        contentDescription = null,
                        modifier = Modifier.size(18.dp),
                    )
                    Spacer(Modifier.size(6.dp))
                    Text(
                        when {
                            state.running -> "暂停"
                            state.remainingSeconds in 1 until state.durationSeconds -> "继续"
                            else -> "开始专注"
                        },
                    )
                }
                OutlinedButton(
                    onClick = {
                        focusTimer.reset()
                        onTimerChanged()
                    },
                    modifier = Modifier.weight(1f),
                ) {
                    Icon(Icons.Outlined.Refresh, contentDescription = null, modifier = Modifier.size(18.dp))
                    Spacer(Modifier.size(6.dp))
                    Text("重置")
                }
            }
        }

        item {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(12.dp),
            ) {
                StatCard("今日专注", formatFocus(state.completedTodaySeconds), Modifier.weight(1f))
                StatCard("本轮时长", "${state.durationSeconds / 60} 分钟", Modifier.weight(1f))
            }
        }

        item {
            Card(
                colors = CardDefaults.cardColors(
                    containerColor = MaterialTheme.colorScheme.surfaceContainer,
                ),
                shape = MaterialTheme.shapes.large,
                modifier = Modifier.fillMaxWidth(),
            ) {
                Column(Modifier.padding(18.dp)) {
                    Text("专注提示", style = MaterialTheme.typography.titleMedium)
                    Spacer(Modifier.height(8.dp))
                    Text(
                        "计时继续在后台运行；开始专注后，房间里的状态会显示计时块。",
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                    Spacer(Modifier.height(10.dp))
                    FilledTonalButton(onClick = {
                        focusTimer.start(45)
                        onTimerChanged()
                    }) {
                        Text("开始 45 分钟")
                    }
                }
            }
        }

        item { Spacer(Modifier.height(72.dp)) }
    }

    if (showDurationDialog) {
        FocusDurationDialog(
            currentMinutes = (state.durationSeconds / 60).coerceIn(1, 180),
            running = state.running,
            onDismiss = { showDurationDialog = false },
            onConfirm = { minutes ->
                focusTimer.setDuration(minutes)
                onTimerChanged()
                showDurationDialog = false
            },
        )
    }
}

@Composable
private fun TimerRing(
    remainingSeconds: Int,
    totalSeconds: Int,
    running: Boolean,
    onClick: () -> Unit,
) {
    val progress = if (totalSeconds <= 0) 0f else remainingSeconds / totalSeconds.toFloat()
    val trackColor = MaterialTheme.colorScheme.surfaceContainerHigh
    val accentColor = MaterialTheme.colorScheme.tertiary
    val interactionSource = remember { MutableInteractionSource() }

    Box(
        modifier = Modifier
            .size(250.dp)
            .clickable(
                interactionSource = interactionSource,
                indication = null,
                onClick = onClick,
            ),
        contentAlignment = Alignment.Center,
    ) {
        Canvas(Modifier.fillMaxSize()) {
            val stroke = 14.dp.toPx()
            val inset = stroke / 2
            drawArc(
                color = trackColor,
                startAngle = -90f,
                sweepAngle = 360f,
                useCenter = false,
                topLeft = Offset(inset, inset),
                size = Size(size.width - stroke, size.height - stroke),
                style = Stroke(width = stroke, cap = StrokeCap.Round),
            )
            drawArc(
                color = accentColor,
                startAngle = -90f,
                sweepAngle = 360f * progress,
                useCenter = false,
                topLeft = Offset(inset, inset),
                size = Size(size.width - stroke, size.height - stroke),
                style = Stroke(width = stroke, cap = StrokeCap.Round),
            )
        }

        Column(horizontalAlignment = Alignment.CenterHorizontally) {
            Text(
                "%02d:%02d".format(remainingSeconds / 60, remainingSeconds % 60),
                fontSize = 54.sp,
                fontWeight = FontWeight.SemiBold,
                color = MaterialTheme.colorScheme.onBackground,
            )
            Text(
                when {
                    running -> "专注中"
                    remainingSeconds == 0 -> "已完成"
                    remainingSeconds < totalSeconds -> "已暂停"
                    else -> "准备开始"
                },
                style = MaterialTheme.typography.labelLarge,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Text(
                "点击时钟设置时长",
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.primary,
                modifier = Modifier.padding(top = 4.dp),
            )
        }
    }
}

@Composable
private fun FocusDurationDialog(
    currentMinutes: Int,
    running: Boolean,
    onDismiss: () -> Unit,
    onConfirm: (Int) -> Unit,
) {
    var minutesText by remember(currentMinutes) { mutableStateOf(currentMinutes.toString()) }
    val minutes = minutesText.toIntOrNull()
    val valid = minutes != null && minutes in 1..180

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("自定义专注时长") },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
                Text(
                    if (running) {
                        "修改后会停止当前专注并重新计时。"
                    } else {
                        "设置后点击「开始专注」即可使用。"
                    },
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    listOf(15, 25, 45).forEach { preset ->
                        FilterChip(
                            selected = minutesText == preset.toString(),
                            onClick = { minutesText = preset.toString() },
                            label = { Text("$preset 分") },
                        )
                    }
                }
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    listOf(60, 90, 120).forEach { preset ->
                        FilterChip(
                            selected = minutesText == preset.toString(),
                            onClick = { minutesText = preset.toString() },
                            label = { Text("$preset 分") },
                        )
                    }
                }
                OutlinedTextField(
                    value = minutesText,
                    onValueChange = { value ->
                        minutesText = value.filter(Char::isDigit).take(3)
                    },
                    label = { Text("分钟") },
                    singleLine = true,
                    isError = minutesText.isNotBlank() && !valid,
                    supportingText = {
                        Text(
                            when {
                                minutesText.isBlank() -> "请输入 1–180 分钟"
                                !valid -> "请输入 1–180 分钟"
                                else -> "本次专注 ${minutes} 分钟"
                            },
                        )
                    },
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                )
            }
        },
        confirmButton = {
            TextButton(
                enabled = valid,
                onClick = { minutes?.let(onConfirm) },
            ) {
                Text(if (running) "停止并设置" else "设置")
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) {
                Text("取消")
            }
        },
    )
}

private fun formatFocus(seconds: Long): String {
    if (seconds <= 0) return "0 分钟"
    val minutes = seconds / 60
    return if (minutes < 60) "$minutes 分钟" else "${minutes / 60} 小时 ${minutes % 60} 分"
}

@Composable
private fun StatCard(label: String, value: String, modifier: Modifier = Modifier) {
    Card(
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceContainer),
        shape = MaterialTheme.shapes.large,
        modifier = modifier,
    ) {
        Column(Modifier.padding(16.dp)) {
            Text(label, style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
            Text(value, style = MaterialTheme.typography.titleLarge, modifier = Modifier.padding(top = 4.dp))
        }
    }
}
