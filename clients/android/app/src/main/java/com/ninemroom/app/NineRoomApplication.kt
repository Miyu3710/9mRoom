package com.ninemroom.app

import android.app.Application

class NineRoomApplication : Application() {
    lateinit var roomController: RoomController
        private set

    override fun onCreate() {
        super.onCreate()
        roomController = RoomController(this)
    }
}
