package com.ninemroom.app

import android.app.Application
import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import androidx.core.content.ContextCompat
import com.ninemroom.app.data.AppCategoryStore
import com.ninemroom.app.data.AppRuleStore
import com.ninemroom.app.data.FocusTimerController
import com.ninemroom.app.data.StatusLayoutStore
import com.ninemroom.app.data.TodoStore

class NineRoomApplication : Application() {
    lateinit var roomController: RoomController
        private set
    lateinit var todoStore: TodoStore
        private set
    lateinit var appRuleStore: AppRuleStore
        private set
    lateinit var appCategoryStore: AppCategoryStore
        private set
    lateinit var statusLayoutStore: StatusLayoutStore
        private set
    lateinit var focusTimer: FocusTimerController
        private set

    private val screenReceiver = object : BroadcastReceiver() {
        override fun onReceive(context: Context?, intent: Intent?) {
            when (intent?.action) {
                Intent.ACTION_SCREEN_OFF,
                Intent.ACTION_SCREEN_ON,
                Intent.ACTION_USER_PRESENT,
                -> roomController.refreshPresence()
            }
        }
    }

    override fun onCreate() {
        super.onCreate()
        todoStore = TodoStore(this)
        appRuleStore = AppRuleStore(this)
        appCategoryStore = AppCategoryStore(this)
        statusLayoutStore = StatusLayoutStore(this)
        focusTimer = FocusTimerController(this)
        roomController = RoomController(
            this,
            todoStore,
            appRuleStore,
            appCategoryStore,
            statusLayoutStore,
            focusTimer,
        )
        ContextCompat.registerReceiver(
            this,
            screenReceiver,
            IntentFilter().apply {
                addAction(Intent.ACTION_SCREEN_OFF)
                addAction(Intent.ACTION_SCREEN_ON)
                addAction(Intent.ACTION_USER_PRESENT)
            },
            ContextCompat.RECEIVER_NOT_EXPORTED,
        )
    }
}
