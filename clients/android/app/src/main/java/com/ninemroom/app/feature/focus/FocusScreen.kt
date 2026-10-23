package com.ninemroom.app.feature.focus

import androidx.compose.animation.animateColorAsState
import androidx.compose.animation.core.animateDpAsState
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Pause
import androidx.compose.material.icons.outlined.PlayArrow
import androidx.compose.material.icons.outlined.Refresh
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.FilterChip
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.ninemroom.app.NineRoomApplication
import com.ninemroom.app.data.FocusPhase

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
                    if (state.isBreak) "休息一下，等下再进入下一轮" else "把注意力留给眼前这一件事",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.padding(top = 4.dp),
                )
            }
        }

        item {
            PhaseSelector(
                focusSelected = !state.isBreak,
                enabled = !state.running,
                onFocus = { focusTimer.setPhase(FocusPhase.FOCUS) },
                onBreak = { focusTimer.setPhase(FocusPhase.BREAK) },
            )
        }

        item {
            TimerRing(
                remainingSeconds = state.remainingSeconds,
                totalSeconds = state.durationSeconds,
                running = state.running,
                phase = state.phase,
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
                            else -> focusTimer.startCurrent()
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
                            state.running -> "暂停${state.phaseName}"
                            state.remainingSeconds in 1 until state.durationSeconds -> "继续${state.phaseName}"
                            else -> "开始${state.phaseName}"
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
                StatCard(
                    if (state.isBreak) "休息时长" else "本轮专注",
                    "${state.durationSeconds / 60} 分钟",
                    Modifier.weight(1f),
                )
            }
        }

        item {
            Text(
                "已完成 ${state.completedRounds} 轮",
                style = MaterialTheme.typography.labelMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }

        item { Spacer(Modifier.height(72.dp)) }
    }

    if (showDurationDialog) {
        FocusDurationDialog(
            focusMinutes = (state.focusDurationSeconds / 60).coerceIn(1, 180),
            breakMinutes = (state.breakDurationSeconds / 60).coerceIn(1, 60),
            cycleEnabled = state.cycleEnabled,
            running = state.running,
            onDismiss = { showDurationDialog = false },
            onConfirm = { focus, rest, cycle ->
                focusTimer.applySettings(focus, rest, cycle)
                onTimerChanged()
                showDurationDialog = false
            },
        )
    }
}

@Composable
private fun PhaseSelector(
    focusSelected: Boolean,
    enabled: Boolean,
    onFocus: () -> Unit,
    onBreak: () -> Unit,
) {
    val focusColor = MaterialTheme.colorScheme.tertiary
    val breakColor = MaterialTheme.colorScheme.primary
    val indicatorColor by animateColorAsState(
        targetValue = if (focusSelected) focusColor else breakColor,
        animationSpec = tween(220),
        label = "phase-indicator-color",
    )
    BoxWithConstraints(
        modifier = Modifier
            .fillMaxWidth()
            .height(44.dp)
            .clip(CircleShape)
            .background(MaterialTheme.colorScheme.surfaceContainerHigh),
    ) {
        val segmentWidth = (maxWidth - 12.dp) / 2
        val indicatorOffset by animateDpAsState(
            targetValue = 4.dp + if (focusSelected) 0.dp else segmentWidth + 4.dp,
            animationSpec = tween(220),
            label = "phase-indicator-offset",
        )
        Box(
            modifier = Modifier
                .offset(x = indicatorOffset, y = 4.dp)
                .width(segmentWidth)
                .height(maxHeight - 8.dp)
                .clip(CircleShape)
                .background(indicatorColor),
        )
        Row(
            modifier = Modifier
                .fillMaxSize()
                .padding(4.dp),
            horizontalArrangement = Arrangement.spacedBy(4.dp),
        ) {
            PhaseSegment(
                label = "专注",
                selected = focusSelected,
                enabled = enabled,
                selectedContentColor = MaterialTheme.colorScheme.onTertiary,
                onClick = onFocus,
                modifier = Modifier.weight(1f),
            )
            PhaseSegment(
                label = "休息",
                selected = !focusSelected,
                enabled = enabled,
                selectedContentColor = MaterialTheme.colorScheme.onPrimary,
                onClick = onBreak,
                modifier = Modifier.weight(1f),
            )
        }
    }
}

@Composable
private fun PhaseSegment(
    label: String,
    selected: Boolean,
    enabled: Boolean,
    selectedContentColor: Color,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
) {
    val contentColor by animateColorAsState(
        targetValue = if (selected) selectedContentColor else MaterialTheme.colorScheme.onSurfaceVariant,
        animationSpec = tween(220),
        label = "phase-label-color",
    )
    Box(
        modifier = modifier
            .fillMaxHeight()
            .clip(CircleShape)
            .clickable(enabled = enabled, onClick = onClick),
        contentAlignment = Alignment.Center,
    ) {
        Text(
            label,
            style = MaterialTheme.typography.titleSmall,
            fontWeight = if (selected) FontWeight.SemiBold else FontWeight.Normal,
            color = contentColor,
        )
    }
}

@Composable
private fun TimerRing(
    remainingSeconds: Int,
    totalSeconds: Int,
    running: Boolean,
    phase: FocusPhase,
    onClick: () -> Unit,
) {
    val progress = if (totalSeconds <= 0) 0f else remainingSeconds / totalSeconds.toFloat()
    val trackColor = MaterialTheme.colorScheme.surfaceContainerHigh
    val accentColor = if (phase == FocusPhase.BREAK) {
        MaterialTheme.colorScheme.primary
    } else {
        MaterialTheme.colorScheme.tertiary
    }
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
                    running && phase == FocusPhase.BREAK -> "休息中"
                    running -> "专注中"
                    remainingSeconds == 0 -> "已完成"
                    remainingSeconds < totalSeconds && phase == FocusPhase.BREAK -> "休息暂停"
                    remainingSeconds < totalSeconds -> "已暂停"
                    phase == FocusPhase.BREAK -> "休息准备"
                    else -> "准备开始"
                },
                style = MaterialTheme.typography.labelLarge,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            if (!running) {
                Text(
                    "点击时钟设置专注与休息",
                    style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.primary,
                    modifier = Modifier.padding(top = 4.dp),
                )
            }
        }
    }
}

@Composable
private fun FocusDurationDialog(
    focusMinutes: Int,
    breakMinutes: Int,
    cycleEnabled: Boolean,
    running: Boolean,
    onDismiss: () -> Unit,
    onConfirm: (focus: Int, rest: Int, cycle: Boolean) -> Unit,
) {
    var focusText by remember(focusMinutes) { mutableStateOf(focusMinutes.toString()) }
    var breakText by remember(breakMinutes) { mutableStateOf(breakMinutes.toString()) }
    var cycle by remember(cycleEnabled) { mutableStateOf(cycleEnabled) }
    val focus = focusText.toIntOrNull()
    val rest = breakText.toIntOrNull()
    val focusValid = focus != null && focus in 1..180
    val breakValid = rest != null && rest in 1..60
    val valid = focusValid && (!cycle || breakValid)

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("专注设置") },
        text = {
            Column(
                modifier = Modifier.verticalScroll(rememberScrollState()),
                verticalArrangement = Arrangement.spacedBy(12.dp),
            ) {
                if (running) {
                    Text(
                        "应用后会停止当前计时。",
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }

                Text("自定义专注时长", style = MaterialTheme.typography.titleSmall)
                listOf(15, 25, 45, 60, 90, 120).chunked(3).forEach { presets ->
                    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        presets.forEach { preset ->
                            FilterChip(
                                selected = focusText == preset.toString(),
                                onClick = { focusText = preset.toString() },
                                label = { Text("$preset 分") },
                            )
                        }
                    }
                }
                OutlinedTextField(
                    value = focusText,
                    onValueChange = { value ->
                        focusText = value.filter(Char::isDigit).take(3)
                    },
                    label = { Text("专注分钟") },
                    singleLine = true,
                    isError = focusText.isNotBlank() && !focusValid,
                    supportingText = {
                        if (focusText.isBlank() || !focusValid) {
                            Text("请输入 1–180 分钟")
                        }
                    },
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                )

                Row(
                    modifier = Modifier.fillMaxWidth(),
                    verticalAlignment = Alignment.CenterVertically,
                ) {
                    Column(Modifier.weight(1f)) {
                        Text(
                            "专注休息时钟循环模式",
                            style = MaterialTheme.typography.titleSmall,
                        )
                        Text(
                            "专注结束后自动开始休息，休息结束后自动开始下一轮专注。",
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                        )
                    }
                    Switch(
                        checked = cycle,
                        onCheckedChange = { cycle = it },
                    )
                }

                if (cycle) {
                    Text("自定义休息时长", style = MaterialTheme.typography.titleSmall)
                    listOf(5, 10, 15, 20, 30, 45).chunked(3).forEach { presets ->
                        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            presets.forEach { preset ->
                                FilterChip(
                                    selected = breakText == preset.toString(),
                                    onClick = { breakText = preset.toString() },
                                    label = { Text("$preset 分") },
                                )
                            }
                        }
                    }
                    OutlinedTextField(
                        value = breakText,
                        onValueChange = { value ->
                            breakText = value.filter(Char::isDigit).take(3)
                        },
                        label = { Text("休息分钟") },
                        singleLine = true,
                        isError = breakText.isNotBlank() && !breakValid,
                        supportingText = {
                            if (breakText.isBlank() || !breakValid) {
                                Text("请输入 1–60 分钟")
                            }
                        },
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                    )
                }
            }
        },
        confirmButton = {
            TextButton(
                enabled = valid,
                onClick = {
                    onConfirm(
                        focus ?: focusMinutes,
                        rest ?: breakMinutes,
                        cycle,
                    )
                },
            ) {
                Text("应用")
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
