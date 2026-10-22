package com.ninemroom.app.feature.apps

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
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
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material.icons.outlined.Add
import androidx.compose.material.icons.outlined.Delete
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.rememberModalBottomSheetState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.ninemroom.app.NineRoomApplication
import com.ninemroom.app.data.AppCategory
import com.ninemroom.app.data.AppCategoryStore

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun CategoryManagerScreen(onBack: () -> Unit) {
    val context = LocalContext.current
    val app = context.applicationContext as NineRoomApplication
    val categoryStore = app.appCategoryStore
    val categories by categoryStore.categories.collectAsStateWithLifecycle()
    var selected by remember { mutableStateOf<AppCategory?>(null) }
    var showAdd by remember { mutableStateOf(false) }
    val sheetState = rememberModalBottomSheetState(skipPartiallyExpanded = true)

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
            Column(Modifier.weight(1f)) {
                Text("分类管理", style = MaterialTheme.typography.headlineSmall)
                Text(
                    "分类模板会作为该分类下应用的默认状态规则。",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
            TextButton(onClick = { showAdd = true }) {
                Icon(Icons.Outlined.Add, contentDescription = null, modifier = Modifier.size(18.dp))
                Spacer(Modifier.size(4.dp))
                Text("新建")
            }
        }

        LazyColumn(
            contentPadding = PaddingValues(start = 20.dp, end = 20.dp, top = 8.dp, bottom = 100.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp),
        ) {
            items(categories, key = { it.id }) { category ->
                CategoryRow(category, onClick = { selected = category })
            }
        }
    }

    if (showAdd) {
        AddCategoryDialog(
            onDismiss = { showAdd = false },
            onAdd = { name, template ->
                categoryStore.add(name, template)
                showAdd = false
            },
        )
    }

    selected?.let { category ->
        ModalBottomSheet(
            onDismissRequest = { selected = null },
            sheetState = sheetState,
        ) {
            CategoryDetail(
                category = categories.firstOrNull { it.id == category.id } ?: category,
                categoryStore = categoryStore,
                appRuleStore = app.appRuleStore,
                onDeleted = { selected = null },
            )
        }
    }
}

@Composable
private fun CategoryRow(category: AppCategory, onClick: () -> Unit) {
    Card(
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceContainer),
        shape = MaterialTheme.shapes.large,
        modifier = Modifier
            .fillMaxWidth()
            .clickable(onClick = onClick),
    ) {
        Row(
            Modifier.padding(horizontal = 14.dp, vertical = 12.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Box(
                Modifier
                    .size(42.dp)
                    .clip(CircleShape)
                    .background(MaterialTheme.colorScheme.secondaryContainer),
                contentAlignment = Alignment.Center,
            ) {
                Text(category.name.take(1), style = MaterialTheme.typography.titleMedium)
            }
            Column(
                Modifier
                    .weight(1f)
                    .padding(start = 12.dp),
            ) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Text(category.name, style = MaterialTheme.typography.bodyLarge)
                    if (!category.builtIn) {
                        Text(
                            "  自定义",
                            style = MaterialTheme.typography.labelMedium,
                            color = MaterialTheme.colorScheme.primary,
                        )
                    }
                }
                Text(
                    category.template,
                    style = MaterialTheme.typography.labelMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis,
                )
            }
        }
    }
}

@Composable
private fun AddCategoryDialog(
    onDismiss: () -> Unit,
    onAdd: (name: String, template: String) -> Unit,
) {
    var name by remember { mutableStateOf("") }
    var template by remember { mutableStateOf("📡 {app}") }
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("新建分类") },
        text = {
            Column {
                OutlinedTextField(
                    value = name,
                    onValueChange = { name = it.take(12) },
                    label = { Text("分类名称") },
                    singleLine = true,
                )
                OutlinedTextField(
                    value = template,
                    onValueChange = { template = it.take(80) },
                    label = { Text("应用模板") },
                    singleLine = true,
                    supportingText = { Text("占位符：{app}") },
                    modifier = Modifier.padding(top = 8.dp),
                )
            }
        },
        confirmButton = {
            TextButton(
                onClick = { onAdd(name, template) },
                enabled = name.isNotBlank(),
            ) { Text("创建") }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) { Text("取消") }
        },
    )
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun CategoryDetail(
    category: AppCategory,
    categoryStore: AppCategoryStore,
    appRuleStore: com.ninemroom.app.data.AppRuleStore,
    onDeleted: () -> Unit,
) {
    var name by remember(category.id) { mutableStateOf(category.name) }
    var template by remember(category.id) { mutableStateOf(category.template) }
    var confirmDelete by remember { mutableStateOf(false) }

    Column(
        Modifier
            .fillMaxWidth()
            .padding(horizontal = 24.dp, vertical = 8.dp),
    ) {
        Text(category.name, style = MaterialTheme.typography.headlineSmall)
        Text(
            if (category.builtIn) "内置分类" else "自定义分类",
            style = MaterialTheme.typography.labelMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )

        OutlinedTextField(
            value = name,
            onValueChange = {
                name = it.take(12)
                categoryStore.update(category.id, name, template)
            },
            label = { Text("分类名称") },
            singleLine = true,
            modifier = Modifier
                .fillMaxWidth()
                .padding(top = 16.dp),
        )
        OutlinedTextField(
            value = template,
            onValueChange = {
                template = it.take(80)
                categoryStore.update(category.id, name, template)
            },
            label = { Text("分类应用模板") },
            singleLine = true,
            supportingText = { Text("应用未单独设置模板时使用；占位符 {app}") },
            modifier = Modifier
                .fillMaxWidth()
                .padding(top = 8.dp),
        )

        if (!category.builtIn) {
            OutlinedButton(
                onClick = { confirmDelete = true },
                modifier = Modifier.padding(top = 14.dp),
            ) {
                Icon(Icons.Outlined.Delete, contentDescription = null, modifier = Modifier.size(18.dp))
                Spacer(Modifier.size(6.dp))
                Text("删除分类")
            }
        }
        Button(
            onClick = onDeleted,
            modifier = Modifier
                .fillMaxWidth()
                .padding(top = 16.dp),
        ) {
            Text("完成")
        }
        Spacer(Modifier.size(20.dp))
    }

    if (confirmDelete) {
        AlertDialog(
            onDismissRequest = { confirmDelete = false },
            title = { Text("删除分类") },
            text = { Text("使用这个分类的应用会回到“其他”，应用规则不会丢失。") },
            confirmButton = {
                TextButton(
                    onClick = {
                        confirmDelete = false
                        if (categoryStore.delete(category.id)) {
                            appRuleStore.clearCategory(category.id)
                            onDeleted()
                        }
                    },
                ) { Text("删除") }
            },
            dismissButton = {
                TextButton(onClick = { confirmDelete = false }) { Text("取消") }
            },
        )
    }
}
