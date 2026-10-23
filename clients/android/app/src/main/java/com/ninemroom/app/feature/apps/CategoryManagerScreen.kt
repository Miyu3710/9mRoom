package com.ninemroom.app.feature.apps

import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.snap
import androidx.compose.animation.core.tween
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.gestures.detectDragGesturesAfterLongPress
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
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material.icons.outlined.Add
import androidx.compose.material.icons.outlined.Delete
import androidx.compose.material.icons.outlined.DragHandle
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
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.alpha
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.dp
import androidx.compose.ui.zIndex
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.ninemroom.app.NineRoomApplication
import com.ninemroom.app.data.AppCategory
import com.ninemroom.app.data.AppCategoryStore
import kotlin.math.roundToInt

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun CategoryManagerScreen(onBack: () -> Unit) {
    val context = LocalContext.current
    val app = context.applicationContext as NineRoomApplication
    val categoryStore = app.appCategoryStore
    val categories by categoryStore.categories.collectAsStateWithLifecycle()
    val layoutRules by app.statusLayoutStore.rules.collectAsStateWithLifecycle()
    val globalAppTemplate = layoutRules.firstOrNull { it.type == "app" }?.template
        ?.takeIf { it.isNotBlank() }
        ?: "📡 {app}"
    var selected by remember { mutableStateOf<AppCategory?>(null) }
    var showAdd by remember { mutableStateOf(false) }
    var draggedId by remember { mutableStateOf<String?>(null) }
    var originIndex by remember { mutableStateOf(-1) }
    var dragY by remember { mutableFloatStateOf(0f) }
    var dragOriginY by remember { mutableFloatStateOf(0f) }
    var targetIndex by remember { mutableStateOf(-1) }
    val listState = rememberLazyListState()
    val density = LocalDensity.current
    val rowHeightPx = with(density) { 76.dp.toPx() }
    val stepPx = with(density) { 84.dp.toPx() }
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
                    "拖拽调整顺序；分类模板留空时使用全局模板。",
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

        Box(
            Modifier
                .fillMaxWidth()
                .weight(1f),
        ) {
            LazyColumn(
                state = listState,
                contentPadding = PaddingValues(start = 20.dp, end = 20.dp, top = 8.dp, bottom = 100.dp),
                verticalArrangement = Arrangement.spacedBy(8.dp),
                modifier = Modifier.fillMaxSize(),
            ) {
                itemsIndexed(categories, key = { _, category -> category.id }) { index, category ->
                    val dragging = draggedId == category.id
                    val shiftSteps = when {
                        dragging || draggedId == null || originIndex < 0 || targetIndex < 0 -> 0
                        targetIndex > originIndex && index in (originIndex + 1)..targetIndex -> -1
                        targetIndex < originIndex && index in targetIndex until originIndex -> 1
                        else -> 0
                    }
                    val shift by animateFloatAsState(
                        targetValue = shiftSteps * stepPx,
                        animationSpec = if (draggedId == null || targetIndex == originIndex) {
                            snap()
                        } else {
                            tween(140)
                        },
                        label = "category-shift",
                    )
                    CategoryRow(
                        category = category,
                        dragging = dragging,
                        hidden = dragging,
                        translationY = shift,
                        onClick = { selected = category },
                        onDragStart = {
                            val info = listState.layoutInfo.visibleItemsInfo
                                .firstOrNull { it.key == category.id }
                            val top = (info?.offset ?: 0) -
                                listState.layoutInfo.viewportStartOffset
                            dragOriginY = top.toFloat()
                            dragY = dragOriginY + rowHeightPx / 2f
                            originIndex = index
                            targetIndex = index
                            draggedId = category.id
                        },
                        onDrag = { amount ->
                            dragY += amount
                            val movedSlots = ((dragY - dragOriginY) / stepPx).roundToInt()
                            targetIndex = (originIndex + movedSlots)
                                .coerceIn(0, categories.lastIndex)
                        },
                        onDragEnd = {
                            val id = draggedId
                            val target = targetIndex
                            if (id != null && target >= 0) {
                                val order = categories.toMutableList()
                                val from = order.indexOfFirst { it.id == id }
                                if (from >= 0) {
                                    val item = order.removeAt(from)
                                    order.add(target.coerceIn(0, order.size), item)
                                    categoryStore.setOrder(order)
                                }
                            }
                            draggedId = null
                            originIndex = -1
                            targetIndex = -1
                            dragY = 0f
                            dragOriginY = 0f
                        },
                    )
                }
            }

            val preview = draggedId?.let { id ->
                categories.firstOrNull { it.id == id }
            }
            if (preview != null) {
                CategoryRow(
                    category = preview,
                    dragging = true,
                    hidden = false,
                    translationY = 0f,
                    interactive = false,
                    onClick = {},
                    onDragStart = {},
                    onDrag = {},
                    onDragEnd = {},
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(horizontal = 20.dp)
                        .offset { IntOffset(0, (dragY - rowHeightPx / 2f).roundToInt()) }
                        .zIndex(10f),
                )
            }
        }
    }

    if (showAdd) {
        AddCategoryDialog(
            globalTemplate = globalAppTemplate,
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
                globalTemplate = globalAppTemplate,
                onDeleted = { selected = null },
            )
        }
    }
}

@Composable
private fun CategoryRow(
    category: AppCategory,
    dragging: Boolean,
    hidden: Boolean = false,
    translationY: Float,
    interactive: Boolean = true,
    onClick: () -> Unit,
    onDragStart: () -> Unit,
    onDrag: (Float) -> Unit,
    onDragEnd: () -> Unit,
    modifier: Modifier = Modifier,
) {
    val interactionSource = remember { MutableInteractionSource() }
    val cardModifier = modifier
        .fillMaxWidth()
        .height(76.dp)
        .zIndex(if (dragging) 10f else 0f)
        .alpha(if (hidden) 0f else 1f)
        .graphicsLayer {
            this.translationY = translationY
        }
    val clickableModifier = if (interactive) {
        cardModifier.clickable(
            interactionSource = interactionSource,
            indication = null,
            onClick = onClick,
        )
    } else {
        cardModifier
    }
    Card(
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceContainer),
        shape = MaterialTheme.shapes.large,
        elevation = CardDefaults.cardElevation(defaultElevation = 0.dp),
        modifier = clickableModifier,
    ) {
        Row(
            Modifier
                .fillMaxSize()
                .padding(horizontal = 10.dp, vertical = 8.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Icon(
                Icons.Outlined.DragHandle,
                contentDescription = "拖拽排序",
                tint = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = if (interactive) {
                    Modifier
                        .padding(horizontal = 4.dp)
                        .pointerInput(category.id) {
                            detectDragGesturesAfterLongPress(
                                onDragStart = { onDragStart() },
                                onDragEnd = { onDragEnd() },
                                onDragCancel = { onDragEnd() },
                                onDrag = { change, amount ->
                                    change.consume()
                                    onDrag(amount.y)
                                },
                            )
                        }
                } else {
                    Modifier.padding(horizontal = 4.dp)
                },
            )
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
                    category.template.ifBlank { "继承全局模板 📡 {app}" },
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
    globalTemplate: String,
    onDismiss: () -> Unit,
    onAdd: (name: String, template: String) -> Unit,
) {
    var name by remember { mutableStateOf("") }
    var template by remember { mutableStateOf("") }
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
                    label = { Text("分类模板") },
                    singleLine = true,
                    placeholder = { Text(globalTemplate) },
                    supportingText = { Text("占位符：{app}；留空则继承全局模板") },
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
    globalTemplate: String,
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
            if (category.builtIn) "默认分类" else "自定义分类",
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
            placeholder = { Text(globalTemplate) },
            supportingText = { Text("应用未单独设置模板时使用；留空则继承全局模板") },
            modifier = Modifier
                .fillMaxWidth()
                .padding(top = 8.dp),
        )

        OutlinedButton(
            onClick = { confirmDelete = true },
            modifier = Modifier.padding(top = 14.dp),
        ) {
            Icon(Icons.Outlined.Delete, contentDescription = null, modifier = Modifier.size(18.dp))
            Spacer(Modifier.size(6.dp))
            Text("删除分类")
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
