package com.ninemroom.app.ui.theme

import android.os.Build
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.dynamicDarkColorScheme
import androidx.compose.material3.dynamicLightColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.platform.LocalContext

private val LightScheme = lightColorScheme(
    primary = CoralPrimary,
    onPrimary = CoralOnPrimary,
    primaryContainer = CoralPrimaryContainer,
    onPrimaryContainer = CoralOnPrimaryContainer,
    secondary = CoralSecondary,
    onSecondary = CoralOnSecondary,
    secondaryContainer = CoralSecondaryContainer,
    onSecondaryContainer = CoralOnSecondaryContainer,
    tertiary = CoralTertiary,
    onTertiary = CoralOnTertiary,
    tertiaryContainer = CoralTertiaryContainer,
    onTertiaryContainer = CoralOnTertiaryContainer,
    error = CoralError,
    onError = CoralOnError,
    errorContainer = CoralErrorContainer,
    onErrorContainer = CoralOnErrorContainer,
    background = LightBackground,
    onBackground = LightOnBackground,
    surface = LightSurface,
    onSurface = LightOnSurface,
    surfaceVariant = LightSurfaceVariant,
    onSurfaceVariant = LightOnSurfaceVariant,
    outline = LightOutline,
    outlineVariant = LightOutlineVariant,
    surfaceContainer = LightSurfaceContainer,
    surfaceContainerHigh = LightSurfaceContainerHigh,
)

private val DarkScheme = darkColorScheme(
    primary = CoralDarkPrimary,
    onPrimary = CoralDarkOnPrimary,
    primaryContainer = CoralDarkPrimaryContainer,
    onPrimaryContainer = CoralDarkOnPrimaryContainer,
    secondary = CoralDarkSecondary,
    onSecondary = CoralDarkOnSecondary,
    secondaryContainer = CoralDarkSecondaryContainer,
    onSecondaryContainer = CoralDarkOnSecondaryContainer,
    tertiary = CoralDarkTertiary,
    onTertiary = CoralDarkOnTertiary,
    tertiaryContainer = CoralDarkTertiaryContainer,
    onTertiaryContainer = CoralDarkOnTertiaryContainer,
    error = CoralDarkError,
    onError = CoralDarkOnError,
    errorContainer = CoralDarkErrorContainer,
    onErrorContainer = CoralDarkOnErrorContainer,
    background = DarkBackground,
    onBackground = DarkOnBackground,
    surface = DarkSurface,
    onSurface = DarkOnSurface,
    surfaceVariant = DarkSurfaceVariant,
    onSurfaceVariant = DarkOnSurfaceVariant,
    outline = DarkOutline,
    outlineVariant = DarkOutlineVariant,
    surfaceContainer = DarkSurfaceContainer,
    surfaceContainerHigh = DarkSurfaceContainerHigh,
)

@Composable
fun NineRoomTheme(
    darkTheme: Boolean = isSystemInDarkTheme(),
    dynamicColor: Boolean = true,
    content: @Composable () -> Unit,
) {
    val context = LocalContext.current
    val scheme = when {
        dynamicColor && Build.VERSION.SDK_INT >= Build.VERSION_CODES.S ->
            if (darkTheme) dynamicDarkColorScheme(context) else dynamicLightColorScheme(context)
        darkTheme -> DarkScheme
        else -> LightScheme
    }

    MaterialTheme(
        colorScheme = scheme,
        typography = NineRoomTypography,
        shapes = NineRoomShapes,
        content = content,
    )
}

