package com.ninemroom.app.feature.todo

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
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
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Add
import androidx.compose.material.icons.outlined.PushPin
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Checkbox
import androidx.compose.material3.FloatingActionButton
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextDecoration
import androidx.compose.ui.unit.dp

data class TodoItem(
    val id: Long,
    val title: String,
    val subtasks: List<String> = emptyList(),
    val completed: Boolean = false,
    val pinned: Boolean = false,
)

@Composable
fun TodoScreen() {
    val todos = remember {
        mutableStateListOf(
            TodoItem(1, "完善 Android 首页", listOf("房间卡片", "成员横条"), pinned = true),
            TodoItem(2, "整理同步列表模板"),
            TodoItem(3, "阅读 Material You 指南"),
        )
    }
    var showAdd by rememberSaveable { mutableStateOf(false) }
    var draft by rememberSaveable { mutableStateOf("") }

    Box(Modifier.fillMaxSize()) {
        LazyColumn(
            contentPadding = PaddingValues(start = 20.dp, end = 20.dp, top = 16.dp, bottom = 100.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            item {
                Column {
                    Text("待办", style = MaterialTheme.typography.headlineMedium)
                    Text(
                        "把下一步写下来，专注会更容易开始。",
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.padding(top = 4.dp),
                    )
                }
            }

            items(todos, key = { it.id }) { todo ->
                TodoCard(
                    todo = todo,
                    onToggle = {
                        val index = todos.indexOfFirst { it.id == todo.id }
                        if (index >= 0) todos[index] = todos[index].copy(completed = !todos[index].completed)
                    },
                    onPin = {
                        val index = todos.indexOfFirst { it.id == todo.id }
                        if (index >= 0) todos[index] = todos[index].copy(pinned = !todos[index].pinned)
                    },
                )
            }
        }

        FloatingActionButton(
            onClick = { showAdd = true },
            modifier = Modifier
                .align(Alignment.BottomEnd)
                .padding(20.dp),
        ) {
            Icon(Icons.Outlined.Add, contentDescription = "添加待办")
        }
    }

    if (showAdd) {
        AlertDialog(
            onDismissRequest = { showAdd = false },
            title = { Text("添加待办") },
            text = {
                OutlinedTextField(
                    value = draft,
                    onValueChange = { draft = it },
                    label = { Text("标题") },
                    singleLine = true,
                )
            },
            confirmButton = {
                TextButton(
                    onClick = {
                        if (draft.isNotBlank()) {
                            todos.add(0, TodoItem(System.currentTimeMillis(), draft.trim()))
                        }
                        draft = ""
                        showAdd = false
                    },
                ) { Text("添加") }
            },
            dismissButton = {
                TextButton(onClick = { showAdd = false }) { Text("取消") }
            },
        )
    }
}

@Composable
private fun TodoCard(
    todo: TodoItem,
    onToggle: () -> Unit,
    onPin: () -> Unit,
) {
    Card(
        colors = CardDefaults.cardColors(
            containerColor = if (todo.pinned) {
                MaterialTheme.colorScheme.secondaryContainer
            } else {
                MaterialTheme.colorScheme.surfaceContainer
            },
        ),
        shape = MaterialTheme.shapes.large,
        modifier = Modifier.fillMaxWidth(),
    ) {
        Row(
            modifier = Modifier.padding(start = 8.dp, end = 8.dp, top = 10.dp, bottom = 10.dp),
            verticalAlignment = Alignment.Top,
        ) {
            Checkbox(checked = todo.completed, onCheckedChange = { onToggle() })
            Column(
                Modifier
                    .weight(1f)
                    .padding(top = 8.dp),
            ) {
                Text(
                    todo.title,
                    style = MaterialTheme.typography.bodyLarge,
                    fontWeight = if (todo.pinned) FontWeight.Medium else FontWeight.Normal,
                    textDecoration = if (todo.completed) TextDecoration.LineThrough else TextDecoration.None,
                    color = if (todo.completed) {
                        MaterialTheme.colorScheme.onSurfaceVariant
                    } else {
                        MaterialTheme.colorScheme.onSurface
                    },
                )
                if (todo.subtasks.isNotEmpty()) {
                    todo.subtasks.forEach { subtask ->
                        Text(
                            "· $subtask",
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                            modifier = Modifier.padding(top = 2.dp),
                        )
                    }
                }
            }
            IconButton(onClick = onPin) {
                Icon(
                    Icons.Outlined.PushPin,
                    contentDescription = if (todo.pinned) "取消置顶" else "置顶",
                    tint = if (todo.pinned) {
                        MaterialTheme.colorScheme.primary
                    } else {
                        MaterialTheme.colorScheme.onSurfaceVariant
                    },
                    modifier = Modifier.size(19.dp),
                )
            }
        }
    }
}

