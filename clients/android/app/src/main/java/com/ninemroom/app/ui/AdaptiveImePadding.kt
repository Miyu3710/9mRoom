package com.ninemroom.app.ui

import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.ime
import androidx.compose.foundation.layout.padding
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.layout.boundsInWindow
import androidx.compose.ui.layout.onGloballyPositioned
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.LocalWindowInfo
import kotlin.math.roundToInt

/**
 * Adds only the part of the IME that actually overlaps this root.
 * The root already sits above the Scaffold bottom bar, so imePadding()
 * would add the keyboard height twice and leave a visible gap.
 */
@Composable
fun Modifier.adaptiveImePadding(): Modifier {
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
