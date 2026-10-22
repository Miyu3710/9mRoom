package com.ninemroom.app.feature.room

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Send
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
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Tab
import androidx.compose.material3.TabRow
import androidx.compose.material3.Text
import androidx.compose.material3.rememberModalBottomSheetState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp

private data class Member(
    val name: String,
    val initial: String,
    val device: String,
    val mode: String,
    val ago: String,
)

private val members = listOf(
    Member("朔月", "朔", "Windows", "专注中", "刚刚"),
    Member("小满", "M", "Android", "自习中", "1 分钟前"),
    Member("阿蓝", "N", "Android", "休息中", "3 分钟前"),
)

private data class LogEntry(val time: String, val text: String)

private val logs = listOf(
    LogEntry("21:32", "朔月 加入了房间"),
    LogEntry("21:33", "小满 进入了专注模式"),
    LogEntry("21:35", "阿蓝 加入了房间"),
)

private data class ChatMessage(val author: String, val text: String)

private val chats = listOf(
    ChatMessage("朔月", "今晚把 Android 首页推起来"),
    ChatMessage("小满", "好，我先看协议部分"),
)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun RoomScreen() {
    var tab by rememberSaveable { mutableIntStateOf(0) }
    var selectedMember by remember { mutableStateOf<Member?>(null) }
    var draft by rememberSaveable { mutableStateOf("") }
    val sheetState = rememberModalBottomSheetState(skipPartiallyExpanded = true)

    Scaffold(
        containerColor = MaterialTheme.colorScheme.background,
        bottomBar = {
            if (tab == 2) {
                ChatComposer(
                    value = draft,
                    onValueChange = { draft = it },
                    onSend = { draft = "" },
                )
            }
        },
    ) { padding ->
        Column(
            Modifier
                .fillMaxSize()
                .padding(padding),
        ) {
            Column(Modifier.padding(horizontal = 20.dp, vertical = 16.dp)) {
                Text("房间", style = MaterialTheme.typography.headlineMedium)
                Text(
                    "鱼高自习室 · 3 人在线",
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

            when (tab) {
                0 -> MemberList(
                    onMemberClick = { selectedMember = it },
                )
                1 -> LogList()
                else -> ChatList()
            }
        }
    }

    selectedMember?.let { member ->
        ModalBottomSheet(
            onDismissRequest = { selectedMember = null },
            sheetState = sheetState,
        ) {
            MemberDetail(member)
        }
    }
}

@Composable
private fun MemberList(onMemberClick: (Member) -> Unit) {
    LazyColumn(
        contentPadding = PaddingValues(20.dp),
        verticalArrangement = Arrangement.spacedBy(10.dp),
    ) {
        items(members) { member ->
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
                    Surface(
                        shape = CircleShape,
                        color = MaterialTheme.colorScheme.secondaryContainer,
                        modifier = Modifier.size(46.dp),
                    ) {
                        Row(
                            Modifier.fillMaxSize(),
                            horizontalArrangement = Arrangement.Center,
                            verticalAlignment = Alignment.CenterVertically,
                        ) {
                            Text(member.initial, style = MaterialTheme.typography.titleMedium)
                        }
                    }
                    Column(
                        Modifier
                            .weight(1f)
                            .padding(start = 12.dp),
                    ) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Text(member.name, style = MaterialTheme.typography.titleMedium)
                            Text(
                                "  ${member.device}",
                                style = MaterialTheme.typography.labelMedium,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                            )
                        }
                        Text(
                            "${member.mode} · 同步于 ${member.ago}",
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                            modifier = Modifier.padding(top = 3.dp),
                        )
                    }
                    Surface(
                        shape = CircleShape,
                        color = MaterialTheme.colorScheme.primary,
                        modifier = Modifier.size(7.dp),
                    ) {}
                }
            }
        }
    }
}

@Composable
private fun LogList() {
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
private fun ChatList() {
    LazyColumn(
        contentPadding = PaddingValues(20.dp),
        verticalArrangement = Arrangement.spacedBy(10.dp),
    ) {
        items(chats) { chat ->
            Card(
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceContainer),
                shape = MaterialTheme.shapes.large,
                modifier = Modifier.fillMaxWidth(),
            ) {
                Column(Modifier.padding(14.dp)) {
                    Text(chat.author, style = MaterialTheme.typography.labelLarge, fontWeight = FontWeight.Medium)
                    Text(chat.text, style = MaterialTheme.typography.bodyLarge, modifier = Modifier.padding(top = 4.dp))
                }
            }
        }
    }
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
                Icon(Icons.Outlined.Send, contentDescription = "发送")
            }
        }
    }
}

@Composable
private fun MemberDetail(member: Member) {
    Column(Modifier.padding(horizontal = 24.dp, vertical = 8.dp)) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Surface(
                shape = CircleShape,
                color = MaterialTheme.colorScheme.secondaryContainer,
                modifier = Modifier.size(58.dp),
            ) {
                Row(
                    Modifier.fillMaxSize(),
                    horizontalArrangement = Arrangement.Center,
                    verticalAlignment = Alignment.CenterVertically,
                ) {
                    Text(member.initial, style = MaterialTheme.typography.headlineSmall)
                }
            }
            Column(Modifier.padding(start = 14.dp)) {
                Text(member.name, style = MaterialTheme.typography.headlineSmall)
                Text(
                    "${member.device} · ${member.mode}",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        }

        Spacer(Modifier.size(22.dp))
        DetailRow("最近同步", member.ago)
        DetailRow("今日专注", "1 小时 24 分")
        DetailRow("状态", "正在被 Android Studio 拷打")
        Spacer(Modifier.size(18.dp))
        Button(onClick = {}, modifier = Modifier.fillMaxWidth()) {
            Text("打个招呼")
        }
        Spacer(Modifier.size(20.dp))
    }
}

@Composable
private fun DetailRow(label: String, value: String) {
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

