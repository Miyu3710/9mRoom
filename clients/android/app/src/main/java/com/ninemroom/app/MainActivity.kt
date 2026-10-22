package com.ninemroom.app

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import com.ninemroom.app.data.AppSettings
import com.ninemroom.app.ui.NineRoomApp
import com.ninemroom.app.ui.theme.NineRoomTheme

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent {
            val settings = remember { AppSettings(applicationContext) }
            var dynamicColor by rememberSaveable { mutableStateOf(settings.dynamicColor) }
            NineRoomTheme(dynamicColor = dynamicColor) {
                NineRoomApp(
                    dynamicColor = dynamicColor,
                    onDynamicColorChange = {
                        settings.dynamicColor = it
                        dynamicColor = it
                    },
                )
            }
        }
    }
}

