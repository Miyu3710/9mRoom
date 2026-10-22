package com.ninemroom.app.feature.apps

import android.content.Intent
import android.content.pm.ApplicationInfo
import android.content.pm.PackageManager
import androidx.compose.foundation.Image
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material.icons.outlined.Search
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilterChip
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Surface
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.rememberModalBottomSheetState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.core.graphics.drawable.toBitmap
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.ninemroom.app.NineRoomApplication
import com.ninemroom.app.data.AppCategory
import com.ninemroom.app.data.AppCategoryStore
import com.ninemroom.app.data.AppRuleStore
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

private data class InstalledApp(
    val packageName: String,
    val label: String,
    val icon: ImageBitmap?,
    val isSystem: Boolean,
)

private fun loadInstalledApps(context: android.content.Context): List<InstalledApp> {
    val manager = context.packageManager
    return manager.getInstalledApplications(PackageManager.MATCH_ALL)
        .asSequence()
        .filter { it.enabled }
        .map { application ->
            val icon = runCatching {
                application.loadIcon(manager).toBitmap(64, 64).asImageBitmap()
            }.getOrNull()
            InstalledApp(
                packageName = application.packageName,
                label = application.loadLabel(manager).toString()
                    .ifBlank { application.packageName },
                icon = icon,
                isSystem = application.flags and
                    (ApplicationInfo.FLAG_SYSTEM or ApplicationInfo.FLAG_UPDATED_SYSTEM_APP) != 0,
            )
        }
        .distinctBy { it.packageName }
        .sortedWith(compareBy<InstalledApp> { it.isSystem }.thenBy { it.label.lowercase() })
        .toList()
}

private enum class AppFilter(val label: String) {
    All("全部"),
    User("用户应用"),
    System("系统应用"),
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun AppRulesScreen(
    onBack: () -> Unit,
    onManageCategories: () -> Unit,
) {
    val context = LocalContext.current
    val app = context.applicationContext as NineRoomApplication
    val store = app.appRuleStore
    val categoryStore = app.appCategoryStore
    val rules by store.rules.collectAsStateWithLifecycle()
    val categories by categoryStore.categories.collectAsStateWithLifecycle()
    var apps by remember { mutableStateOf<List<InstalledApp>>(emptyList()) }
    var loading by remember { mutableStateOf(true) }
    var query by remember { mutableStateOf("") }
    var filter by remember { mutableStateOf(AppFilter.All) }
    var selected by remember { mutableStateOf<InstalledApp?>(null) }
    val sheetState = rememberModalBottomSheetState(skipPartiallyExpanded = true)

    LaunchedEffect(Unit) {
        apps = withContext(Dispatchers.IO) { loadInstalledApps(context) }
        loading = false
    }

    val filtered = remember(query, apps, filter) {
        apps.filter { app ->
            when (filter) {
                AppFilter.All -> true
                AppFilter.User -> !app.isSystem
                AppFilter.System -> app.isSystem
            }
        }.filter {
            query.isBlank() ||
                it.label.contains(query, ignoreCase = true) ||
                it.packageName.contains(query, ignoreCase = true)
        }
    }

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
                Text("应用与分类", style = MaterialTheme.typography.headlineSmall)
                Text(
                    "点开应用可改分类、显示名称和共享开关；规则只保存在本机。",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
            TextButton(onClick = onManageCategories) {
                Text("管理分类")
            }
        }

        OutlinedTextField(
            value = query,
            onValueChange = { query = it },
            placeholder = { Text("搜索应用或包名") },
            leadingIcon = { Icon(Icons.Outlined.Search, contentDescription = null) },
            singleLine = true,
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 20.dp, vertical = 8.dp),
        )

        Row(
            Modifier
                .fillMaxWidth()
                .padding(horizontal = 20.dp, vertical = 2.dp),
            horizontalArrangement = Arrangement.spacedBy(8.dp),
        ) {
            AppFilter.entries.forEach { option ->
                FilterChip(
                    selected = filter == option,
                    onClick = { filter = option },
                    label = { Text(option.label) },
                )
            }
        }

        if (loading) {
            Box(
                Modifier
                    .fillMaxWidth()
                    .padding(32.dp),
                contentAlignment = Alignment.Center,
            ) {
                CircularProgressIndicator(modifier = Modifier.size(28.dp))
            }
        }

        LazyColumn(
            contentPadding = PaddingValues(start = 20.dp, end = 20.dp, top = 4.dp, bottom = 100.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp),
        ) {
            items(filtered, key = { it.packageName }) { app ->
                val rule = rules[app.packageName]
                AppRow(
                    app = app,
                    displayName = rule?.displayName ?: app.label,
                    categoryName = categories.firstOrNull { it.id == (rule?.category ?: "other") }?.name
                        ?: "其他",
                    hidden = rule?.hidden == true,
                    onClick = { selected = app },
                )
            }
        }
    }

    selected?.let { app ->
        ModalBottomSheet(
            onDismissRequest = { selected = null },
            sheetState = sheetState,
        ) {
            AppDetail(
                app = app,
                store = store,
                categoryStore = categoryStore,
                categories = categories,
                onDismiss = { selected = null },
            )
        }
    }
}

@Composable
private fun AppRow(
    app: InstalledApp,
    displayName: String,
    categoryName: String,
    hidden: Boolean,
    onClick: () -> Unit,
) {
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
            AppIcon(app)
            Column(
                Modifier
                    .weight(1f)
                    .padding(start = 12.dp, end = 8.dp),
            ) {
                Text(
                    displayName,
                    style = MaterialTheme.typography.bodyLarge,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis,
                )
                Text(
                    "${categoryName}${if (app.isSystem) " · 系统" else ""} · ${app.packageName}",
                    style = MaterialTheme.typography.labelMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis,
                )
            }
            Text(
                if (hidden) "已隐藏" else "共享中",
                style = MaterialTheme.typography.labelMedium,
                color = if (hidden) MaterialTheme.colorScheme.error else MaterialTheme.colorScheme.primary,
            )
        }
    }
}

@Composable
private fun AppIcon(app: InstalledApp) {
    if (app.icon != null) {
        Image(
            bitmap = app.icon,
            contentDescription = null,
            contentScale = ContentScale.Fit,
            modifier = Modifier
                .size(44.dp)
                .clip(RoundedCornerShape(12.dp)),
        )
    } else {
        Surface(
            shape = RoundedCornerShape(12.dp),
            color = MaterialTheme.colorScheme.secondaryContainer,
            modifier = Modifier.size(44.dp),
        ) {
            Box(contentAlignment = Alignment.Center) {
                Text(app.label.take(1), style = MaterialTheme.typography.titleMedium)
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun AppDetail(
    app: InstalledApp,
    store: AppRuleStore,
    categoryStore: AppCategoryStore,
    categories: List<AppCategory>,
    onDismiss: () -> Unit,
) {
    val rules by store.rules.collectAsStateWithLifecycle()
    val rule = rules[app.packageName]
    var name by remember(app.packageName) {
        mutableStateOf(rule?.displayName ?: "")
    }
    var template by remember(app.packageName) {
        mutableStateOf(rule?.template ?: "")
    }

    Column(
        Modifier
            .fillMaxWidth()
            .verticalScroll(rememberScrollState())
            .padding(horizontal = 24.dp, vertical = 8.dp),
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            AppIcon(app)
            Column(Modifier.padding(start = 14.dp)) {
                Text(
                    rule?.displayName ?: app.label,
                    style = MaterialTheme.typography.headlineSmall,
                )
                Text(
                    app.packageName,
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        }

        Spacer(Modifier.size(22.dp))
        Text("显示名称", style = MaterialTheme.typography.titleMedium)
        Text(
            "会随状态块发送；接收端支持时显示这个名称，本机列表始终使用它。",
            style = MaterialTheme.typography.labelMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.padding(top = 2.dp, bottom = 6.dp),
        )
        OutlinedTextField(
            value = name,
            onValueChange = {
                name = it.take(24)
                store.setDisplayName(app.packageName, name)
            },
            placeholder = { Text(app.label) },
            singleLine = true,
            modifier = Modifier.fillMaxWidth(),
        )

        Spacer(Modifier.size(18.dp))
        Text("分类", style = MaterialTheme.typography.titleMedium)
        FlowRow(
            horizontalArrangement = Arrangement.spacedBy(8.dp),
            modifier = Modifier.padding(top = 6.dp),
        ) {
            categories.forEach { category ->
                FilterChip(
                    selected = (rule?.category ?: "other") == category.id,
                    onClick = { store.setCategory(app.packageName, category.id) },
                    label = { Text(category.name) },
                )
            }
        }

        Spacer(Modifier.size(16.dp))
        Text("应用模板", style = MaterialTheme.typography.titleMedium)
        Text(
            "留空时继承分类模板；支持 {app} 占位符。",
            style = MaterialTheme.typography.labelMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.padding(top = 2.dp, bottom = 6.dp),
        )
        OutlinedTextField(
            value = template,
            onValueChange = {
                template = it.take(80)
                store.setTemplate(app.packageName, template)
            },
            placeholder = {
                Text(
                    categoryStore.find(rule?.category ?: "other")?.template
                        ?: "📡 {app}",
                )
            },
            singleLine = true,
            supportingText = {
                Text("优先于分类模板；清空后恢复继承。")
            },
            modifier = Modifier.fillMaxWidth(),
        )

        Spacer(Modifier.size(16.dp))
        Row(
            Modifier.fillMaxWidth(),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Column(Modifier.weight(1f)) {
                Text("共享此应用", style = MaterialTheme.typography.bodyLarge)
                Text(
                    "关闭后，当前应用不会出现在房间状态里",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
            Switch(
                checked = rule?.hidden != true,
                onCheckedChange = { store.setHidden(app.packageName, !it) },
            )
        }

        Button(
            onClick = onDismiss,
            modifier = Modifier
                .fillMaxWidth()
                .padding(top = 20.dp),
        ) {
            Text("完成")
        }
        Spacer(Modifier.size(20.dp))
    }
}
