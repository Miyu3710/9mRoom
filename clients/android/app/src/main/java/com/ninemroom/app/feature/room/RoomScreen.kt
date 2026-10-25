package com.ninemroom.app.feature.room

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.ime
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.Send
import androidx.compose.material.icons.outlined.PhotoCamera
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Surface
import androidx.compose.material3.Tab
import androidx.compose.material3.TabRow
import androidx.compose.material3.Text
import androidx.compose.material3.rememberModalBottomSheetState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.layout.boundsInWindow
import androidx.compose.ui.layout.onGloballyPositioned
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.LocalWindowInfo
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import com.ninemroom.app.data.ChatLine
import com.ninemroom.app.data.LogLine
import com.ninemroom.app.data.RoomMember
import com.ninemroom.app.data.chatTime
import com.ninemroom.app.ui.MemberAvatar
import kotlin.math.roundToInt

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun RoomScreen(
    roomName: String,
    members: List<RoomMember>,
    selfId: String?,
    chat: List<ChatLine>,
    logs: List<LogLine>,
    onSendChat: (String) -> Unit,
    onPoke: (String) -> Unit,
    onChangeAvatar: () -> Unit,
) {
    var tab by rememberSaveable { mutableIntStateOf(0) }
    var selectedMember by remember { mutableStateOf<RoomMember?>(null) }
    var draft by rememberSaveable { mutableStateOf("") }
    val sheetState = rememberModalBottomSheetState(skipPartiallyExpanded = true)
    val onlineCount = members.count { it.online }

    Column(
        Modifier
            .fillMaxSize()
            .adaptiveImePadding(),
    ) {
        Column(Modifier.padding(horizontal = 20.dp, vertical = 16.dp)) {
            Text("房间", style = MaterialTheme.typography.headlineMedium)
            Text(
                "${roomName.ifBlank { "未命名房间" }} · ${onlineCount} 人在线",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(top = 4.dp),
            )
        }

        TabRow(
            selectedTabIndex = tab,
            containerColor = MaterialTheme.colorScheme.background,
        ) {
            listOf("成员", "日志", "聊天").forEachIndexed { index, label ->
                Tab(
                    selected = tab == index,
                    onClick = { tab = index },
                    text = { Text(label) },
                )
            }
        }

        Box(Modifier.weight(1f)) {
            when (tab) {
                0 -> MemberList(members, selfId) { selectedMember = it }
                1 -> LogList(logs)
                else -> ChatList(chat, selfId, members) { selectedMember = it }
            }
        }

        if (tab == 2) {
            ChatComposer(
                value = draft,
                onValueChange = { draft = it },
                onSend = {
                    val message = draft.trim()
                    if (message.isNotBlank()) {
                        onSendChat(message)
                        draft = ""
                    }
                },
            )
        }
    }

    selectedMember?.let { member ->
        ModalBottomSheet(
            onDismissRequest = { selectedMember = null },
            sheetState = sheetState,
        ) {
            MemberDetail(
                member = member,
                isSelf = member.id == selfId,
                onChangeAvatar = if (member.id == selfId) {
                    {
                        selectedMember = null
                        onChangeAvatar()
                    }
                } else {
                    null
                },
                onPoke = { onPoke(member.id) },
            )
        }
    }
}

@Composable
private fun MemberList(
    members: List<RoomMember>,
    selfId: String?,
    onMemberClick: (RoomMember) -> Unit,
) {
    if (members.isEmpty()) {
        EmptyState("正在等待成员同步…")
        return
    }

    LazyColumn(
        contentPadding = PaddingValues(20.dp),
        verticalArrangement = Arrangement.spacedBy(10.dp),
    ) {
        items(members, key = { it.id }) { member ->
            Card(
                onClick = { onMemberClick(member) },
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceContainer),
                shape = MaterialTheme.shapes.large,
                modifier = Modifier.fillMaxWidth(),
            ) {
                Row(
                    Modifier.padding(14.dp),
                    verticalAlignment = Alignment.CenterVertically,
                ) {
                    MemberAvatar(
                        avatar = member.avatar,
                        initial = member.initial,
                        size = 46.dp,
                    )
                    Column(
                        Modifier
                            .weight(1f)
                            .padding(start = 12.dp),
                    ) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Text(
                                if (member.id == selfId) "${member.name}（我）" else member.name,
                                style = MaterialTheme.typography.titleMedium,
                            )
                            Text(
                                "  ${member.deviceLabel}",
                                style = MaterialTheme.typography.labelMedium,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                            )
                        }
                        Text(
                            if (member.isReconnecting) {
                                "重连中 · ${member.modeLabel}"
                            } else if (!member.online) {
                                "离线"
                            } else {
                                "${member.modeLabel} · 同步于 ${member.agoText}"
                            },
                            style = MaterialTheme.typography.bodyMedium,
                            color = if (member.isReconnecting) {
                                MaterialTheme.colorScheme.tertiary
                            } else {
                                MaterialTheme.colorScheme.onSurfaceVariant
                            },
                            modifier = Modifier.padding(top = 3.dp),
                        )
                        if (member.statusText.isNotBlank()) {
                            Text(
                                member.statusText,
                                style = MaterialTheme.typography.bodyMedium,
                                color = MaterialTheme.colorScheme.onSurface,
                                maxLines = 2,
                                overflow = TextOverflow.Ellipsis,
                                modifier = Modifier.padding(top = 3.dp),
                            )
                        }
                    }
                    Surface(
                        shape = CircleShape,
                        color = if (member.isReconnecting) {
                            MaterialTheme.colorScheme.tertiary
                        } else if (member.online) {
                            MaterialTheme.colorScheme.primary
                        } else {
                            MaterialTheme.colorScheme.outlineVariant
                        },
                        modifier = Modifier.size(7.dp),
                    ) {}
                }
            }
        }
    }
}

@Composable
private fun LogList(logs: List<LogLine>) {
    if (logs.isEmpty()) {
        EmptyState("还没有房间事件")
        return
    }

    LazyColumn(
        contentPadding = PaddingValues(20.dp),
        verticalArrangement = Arrangement.spacedBy(2.dp),
    ) {
        items(logs) { log ->
            Row(
                Modifier
                    .fillMaxWidth()
                    .padding(vertical = 9.dp),
            ) {
                Text(
                    log.time,
                    style = MaterialTheme.typography.labelMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.padding(end = 14.dp),
                )
                Text(log.text, style = MaterialTheme.typography.bodyMedium)
            }
            HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.5f))
        }
    }
}

@Composable
private fun ChatList(
    chat: List<ChatLine>,
    selfId: String?,
    members: List<RoomMember>,
    onMemberClick: (RoomMember) -> Unit,
) {
    if (chat.isEmpty()) {
        EmptyState("还没有消息，打个招呼吧")
        return
    }

    val listState = rememberLazyListState()
    var previousSize by remember { mutableIntStateOf(chat.size) }
    var previousLatestId by remember { mutableStateOf(chat.lastOrNull()?.id) }

    LaunchedEffect(chat.size) {
        val oldLatestVisible = listState.layoutInfo.visibleItemsInfo
            .any { it.key == previousLatestId }
        if (chat.size > previousSize && oldLatestVisible) {
            listState.scrollToItem(0)
        }
        previousSize = chat.size
        previousLatestId = chat.lastOrNull()?.id
    }

    LazyColumn(
        state = listState,
        reverseLayout = true,
        contentPadding = PaddingValues(20.dp),
        verticalArrangement = Arrangement.spacedBy(10.dp),
    ) {        items(chat.asReversed(), key = { it.id }) { line ->
            val mine = line.memberId == selfId
            val member = members.firstOrNull { it.id == line.memberId }
            val avatar = member?.avatar
            val initial = member?.initial ?: line.author.take(1).ifBlank { "?" }

            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(vertical = 2.dp),
                horizontalArrangement = if (mine) Arrangement.End else Arrangement.Start,
                verticalAlignment = Alignment.Top,
            ) {
                if (!mine) {
                    MemberAvatar(
                        avatar = avatar,
                        initial = initial,
                        size = 34.dp,
                        onClick = member?.let { { onMemberClick(it) } },
                    )
                    Spacer(Modifier.size(8.dp))
                }

                Card(
                    colors = CardDefaults.cardColors(
                        containerColor = if (mine) {
                            MaterialTheme.colorScheme.primaryContainer
                        } else {
                            MaterialTheme.colorScheme.surfaceContainer
                        },
                    ),
                    shape = MaterialTheme.shapes.large,
                    modifier = Modifier.widthIn(max = 300.dp),
                ) {
                    Column(Modifier.padding(14.dp)) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Text(
                                line.author,
                                style = MaterialTheme.typography.labelLarge,
                                fontWeight = FontWeight.Medium,
                                modifier = Modifier.weight(1f),
                            )
                            Text(
                                chatTime(line.at),
                                style = MaterialTheme.typography.labelMedium,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                            )
                        }
                        Text(
                            line.text,
                            style = MaterialTheme.typography.bodyLarge,
                            modifier = Modifier.padding(top = 4.dp),
                        )
                    }
                }

                if (mine) {
                    Spacer(Modifier.size(8.dp))
                    MemberAvatar(
                        avatar = avatar,
                        initial = initial,
                        size = 34.dp,
                        onClick = member?.let { { onMemberClick(it) } },
                    )
                }
            }
        }
    }
}

@Composable
private fun EmptyState(text: String) {
    Box(
        Modifier
            .fillMaxSize()
            .padding(32.dp),
        contentAlignment = Alignment.Center,
    ) {
        Text(
            text,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}

@Composable
private fun Modifier.adaptiveImePadding(): Modifier {
    val density = LocalDensity.current
    val windowInfo = LocalWindowInfo.current
    val imeBottom = WindowInsets.ime.getBottom(density)
    var rootBottom by remember { mutableIntStateOf(0) }
    val bottomPadding = (
        rootBottom - (windowInfo.containerSize.height - imeBottom)
        ).coerceAtLeast(0)

    return this
        .onGloballyPositioned {
            rootBottom = it.boundsInWindow().bottom.roundToInt()
        }
        .padding(bottom = with(density) { bottomPadding.toDp() })
}

@Composable
private fun ChatComposer(
    value: String,
    onValueChange: (String) -> Unit,
    onSend: () -> Unit,
) {
    Surface(color = MaterialTheme.colorScheme.surfaceContainer) {
        Row(
            Modifier
                .fillMaxWidth()
                .padding(horizontal = 14.dp, vertical = 10.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            OutlinedTextField(
                value = value,
                onValueChange = onValueChange,
                placeholder = { Text("说点什么…") },
                modifier = Modifier.weight(1f),
                maxLines = 4,
                shape = MaterialTheme.shapes.extraLarge,
            )
            Spacer(Modifier.size(8.dp))
            IconButton(onClick = onSend, enabled = value.isNotBlank()) {
                Icon(Icons.AutoMirrored.Outlined.Send, contentDescription = "发送")
            }
        }
    }
}

@Composable
internal fun MemberDetail(
    member: RoomMember,
    isSelf: Boolean,
    onChangeAvatar: (() -> Unit)? = null,
    onPoke: () -> Unit,
) {
    Column(Modifier.padding(horizontal = 24.dp, vertical = 8.dp)) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Box {
                MemberAvatar(
                    avatar = member.avatar,
                    initial = member.initial,
                    size = 58.dp,
                    onClick = if (isSelf) onChangeAvatar else null,
                )
                if (isSelf && onChangeAvatar != null) {
                    Surface(
                        shape = CircleShape,
                        color = MaterialTheme.colorScheme.surfaceContainerHighest,
                        modifier = Modifier
                            .align(Alignment.BottomEnd)
                            .size(22.dp),
                    ) {
                        Icon(
                            Icons.Outlined.PhotoCamera,
                            contentDescription = "更换头像",
                            modifier = Modifier
                                .fillMaxSize()
                                .padding(4.dp),
                        )
                    }
                }
            }
            Column(Modifier.padding(start = 14.dp)) {
                Text(
                    if (isSelf) "${member.name}（我）" else member.name,
                    style = MaterialTheme.typography.headlineSmall,
                )
                Text(
                    if (member.isReconnecting) {
                        "${member.deviceLabel} · 重连中"
                    } else {
                        "${member.deviceLabel} · ${member.modeLabel}"
                    },
                    style = MaterialTheme.typography.bodyMedium,
                    color = if (member.isReconnecting) {
                        MaterialTheme.colorScheme.tertiary
                    } else {
                        MaterialTheme.colorScheme.onSurfaceVariant
                    },
                )
            }
        }

        Spacer(Modifier.size(22.dp))
        DetailRow("最近同步", member.agoText)
        DetailRow("今日专注", formatFocus(member.focusSecondsToday))
        DetailRow("状态", member.statusText.ifBlank { "未共享状态" }, stacked = true)
        Spacer(Modifier.size(18.dp))
        Button(
            onClick = onPoke,
            enabled = !isSelf && member.online,
            modifier = Modifier.fillMaxWidth(),
        ) {
            Text("戳一下")
        }
        Spacer(Modifier.size(20.dp))
    }
}

private fun formatFocus(seconds: Long): String {
    if (seconds <= 0) return "0 分钟"
    val minutes = seconds / 60
    return if (minutes < 60) "$minutes 分钟" else "${minutes / 60} 小时 ${minutes % 60} 分"
}

@Composable
private fun DetailRow(label: String, value: String, stacked: Boolean = false) {
    if (stacked) {
        Column(
            Modifier
                .fillMaxWidth()
                .padding(vertical = 7.dp),
        ) {
            Text(
                label,
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Text(
                value,
                style = MaterialTheme.typography.bodyMedium,
                modifier = Modifier.padding(top = 3.dp),
            )
        }
        return
    }
    Row(
        Modifier
            .fillMaxWidth()
            .padding(vertical = 7.dp),
    ) {
        Text(
            label,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.weight(1f),
        )
        Text(value, style = MaterialTheme.typography.bodyMedium)
    }
}
