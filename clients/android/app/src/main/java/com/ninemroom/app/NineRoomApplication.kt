package com.ninemroom.app

import android.app.Application
import com.ninemroom.app.data.AppRuleStore
import com.ninemroom.app.data.FocusTimerController
import com.ninemroom.app.data.TodoStore

class NineRoomApplication : Application() {
    lateinit var roomController: RoomController
        private set
    lateinit var todoStore: TodoStore
        private set
    lateinit var appRuleStore: AppRuleStore
        private set
    lateinit var focusTimer: FocusTimerController
        private set

    override fun onCreate() {
        super.onCreate()
        todoStore = TodoStore(this)
        appRuleStore = AppRuleStore(this)
        focusTimer = FocusTimerController(this)
        roomController = RoomController(this, todoStore, appRuleStore, focusTimer)
    }
}
