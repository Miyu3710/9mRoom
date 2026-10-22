package com.ninemroom.app

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import com.ninemroom.app.data.ConnectionStatus
import com.ninemroom.app.data.RoomMember
import kotlinx.coroutines.flow.StateFlow

class RoomViewModel(application: Application) : AndroidViewModel(application) {
    private val controller = (application as NineRoomApplication).roomController

    val state: StateFlow<RoomUiState> = controller.state

    fun connect(server: String, room: String, name: String) =
        controller.connect(server, room, name)

    fun retry() = controller.retry()

    fun setMode(mode: String) = controller.setMode(mode)

    fun sendChat(text: String) = controller.sendChat(text)

    fun disconnect() = controller.disconnect()

    fun setKeepOnline(enabled: Boolean) = controller.setKeepOnline(enabled)

    fun setOverlayEnabled(enabled: Boolean) = controller.setOverlayEnabled(enabled)

    fun setNotifyRoomEvents(enabled: Boolean) = controller.setNotifyRoomEvents(enabled)

    fun refreshOverlay() = controller.refreshOverlay()
}
