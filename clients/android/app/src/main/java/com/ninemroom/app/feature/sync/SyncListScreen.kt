package com.ninemroom.app.feature.sync

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.foundation.clickable
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
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material.icons.outlined.Add
import androidx.compose.material.icons.outlined.Delete
import androidx.compose.material.icons.outlined.DragHandle
import androidx.compose.material.icons.outlined.ExpandLess
import androidx.compose.material.icons.outlined.ExpandMore
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.ninemroom.app.NineRoomApplication
import com.ninemroom.app.data.StatusBlockRule
import com.ninemroom.app.data.StatusLayoutStore
import com.ninemroom.app.data.statusBlockTypes
import sh.calvin.reorderable.ReorderableItem
import sh.calvin.reorderable.rememberReorderableLazyListState

@Composable
fun SyncListScreen(onBack: () -> Unit) {
    val context = LocalContext.current
    val store = (context.applicationContext as NineRoomApplication).statusLayoutStore
    val rules by store.rules.collectAsStateWithLifecycle()
    val listState = rememberLazyListState()
    val reorderState = rememberReorderableLazyListState(listState) { from, to ->
        store.move(from.index, to.index)
    }

    var expandedId by remember { mutableStateOf<String?>(null) }
    var addMenuOpen by remember { mutableStateOf(false) }

    Column(Modifier.fillMaxSize()) {
        Row(
            Modifier
                .fillMaxWidth()
                .padding(start = 8.dp, end = 20.dp, top = 8.dp, bottom = 4.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            IconButton(onClick = onBack) {
                Icon(Icons.AutoMirrored.Outlined.ArrowBack, contentDescription = "返回")
            }
            Column {
                Text("同步列表", style = MaterialTheme.typography.headlineSmall)
                Text(
                    "长按拖拽排序，点开项目编辑模板。",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        }

        LazyColumn(
            state = listState,
            contentPadding = PaddingValues(start = 20.dp, end = 20.dp, top = 8.dp, bottom = 120.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp),
            modifier = Modifier.weight(1f),
        ) {
            items(rules, key = { it.id }) { rule ->
                ReorderableItem(
                    state = reorderState,
                    key = rule.id,
                ) { _ ->
                    StatusBlockCard(
                        rule = rule,
                        expanded = expandedId == rule.id,
                        onToggle = { store.setEnabled(rule.id, it) },
                        onExpand = {
                            expandedId = if (expandedId == rule.id) null else rule.id
                        },
                        onDelete = {
                            store.remove(rule.id)
                            if (expandedId == rule.id) expandedId = null
                        },
                        onTemplateChange = { store.updateTemplate(rule.id, it) },
                        dragHandleModifier = Modifier.longPressDraggableHandle(),
                    )
                }
            }
        }

        Box(
            Modifier
                .fillMaxWidth()
                .padding(horizontal = 20.dp, vertical = 12.dp),
        ) {
            Button(
                onClick = { addMenuOpen = true },
                modifier = Modifier.fillMaxWidth(),
            ) {
                Icon(Icons.Outlined.Add, contentDescription = null, modifier = Modifier.size(18.dp))
                Spacer(Modifier.size(6.dp))
                Text("添加状态块")
            }
            DropdownMenu(
                expanded = addMenuOpen,
                onDismissRequest = { addMenuOpen = false },
            ) {
                statusBlockTypes.forEach { (type, label) ->
                    DropdownMenuItem(
                        text = { Text(label) },
                        onClick = {
                            expandedId = store.add(type)
                            addMenuOpen = false
                        },
                    )
                }
            }
        }
    }
}

@Composable
private fun StatusBlockCard(
    rule: StatusBlockRule,
    expanded: Boolean,
    onToggle: (Boolean) -> Unit,
    onExpand: () -> Unit,
    onDelete: () -> Unit,
    onTemplateChange: (String) -> Unit,
    dragHandleModifier: Modifier,
) {
    Card(
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.surfaceContainer,
        ),
        shape = MaterialTheme.shapes.large,
        elevation = CardDefaults.cardElevation(defaultElevation = 0.dp),
        modifier = Modifier.fillMaxWidth(),
    ) {
        Column {
            Row(
                Modifier
                    .fillMaxWidth()
                    .height(84.dp)
                    .clickable(onClick = onExpand)
                    .padding(horizontal = 8.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Icon(
                    Icons.Outlined.DragHandle,
                    contentDescription = "拖拽排序",
                    tint = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier
                        .padding(horizontal = 6.dp)
                        .then(dragHandleModifier),
                )
                Column(Modifier.weight(1f)) {
                    Text(
                        statusBlockTypes[rule.type] ?: rule.type,
                        style = MaterialTheme.typography.bodyLarge,
                    )
                    Text(
                        rule.template,
                        style = MaterialTheme.typography.labelMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        maxLines = 1,
                    )
                }
                Switch(checked = rule.enabled, onCheckedChange = onToggle)
                IconButton(onClick = onExpand) {
                    Icon(
                        if (expanded) Icons.Outlined.ExpandLess else Icons.Outlined.ExpandMore,
                        contentDescription = if (expanded) "收起" else "展开",
                    )
                }
                IconButton(onClick = onDelete) {
                    Icon(Icons.Outlined.Delete, contentDescription = "删除")
                }
            }

            AnimatedVisibility(visible = expanded) {
                Column(
                    Modifier.padding(start = 18.dp, end = 18.dp, bottom = 16.dp),
                ) {
                    OutlinedTextField(
                        value = rule.template,
                        onValueChange = onTemplateChange,
                        label = { Text("模板") },
                        supportingText = {
                            Text(
                                when (rule.type) {
                                    "app" -> "占位符：{app}"
                                    "media" -> "占位符：{title} {artist} {progress}"
                                    "todo" -> "占位符：{todo}"
                                    "timer" -> "占位符：{remaining}"
                                    else -> "自定义文本会原样显示"
                                },
                            )
                        },
                        minLines = 2,
                        modifier = Modifier.fillMaxWidth(),
                    )
                    Text(
                        "内容来自本机；服务端只接收渲染所需的块数据。",
                        style = MaterialTheme.typography.labelMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.padding(top = 4.dp),
                    )
                }
            }
        }
    }
}
