package com.ninemroom.app.service

import android.app.Service
import android.content.Context
import android.content.Intent
import android.content.pm.ServiceInfo
import android.graphics.Color
import android.graphics.PixelFormat
import android.graphics.drawable.GradientDrawable
import android.os.Build
import android.os.IBinder
import android.provider.Settings
import android.view.Gravity
import android.view.MotionEvent
import android.view.View
import android.view.WindowManager
import android.widget.TextView
import androidx.core.app.ServiceCompat
import com.ninemroom.app.NineRoomApplication
import com.ninemroom.app.RoomUiState
import com.ninemroom.app.data.ConnectionStatus
import com.ninemroom.app.data.modeLabel
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.flow.collectLatest
import kotlinx.coroutines.launch

class PresenceService : Service() {
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main.immediate)
    private var collectJob: Job? = null
    private var overlayView: TextView? = null
    private var overlayParams: WindowManager.LayoutParams? = null
    private var waitForConnection = false

    override fun onCreate() {
        super.onCreate()
        NotificationCenter.ensureChannels(this)
        val controller = (application as NineRoomApplication).roomController
        ServiceCompat.startForeground(
            this,
            NotificationCenter.PRESENCE_ID,
            NotificationCenter.presence(this, controller.state.value),
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.UPSIDE_DOWN_CAKE) {
                ServiceInfo.FOREGROUND_SERVICE_TYPE_SPECIAL_USE
            } else {
                0
            },
        )
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        waitForConnection = intent?.getBooleanExtra(EXTRA_WAIT_FOR_CONNECTION, false) == true
        startObserving()
        val state = (application as NineRoomApplication).roomController.state.value
        updateOverlay(state)
        return START_STICKY
    }

    private fun startObserving() {
        if (collectJob != null) return
        val controller = (application as NineRoomApplication).roomController
        collectJob = scope.launch {
            controller.state.collectLatest { state ->
                if (state.status == ConnectionStatus.Connected) {
                    waitForConnection = false
                }
                if ((state.status == ConnectionStatus.Idle && !waitForConnection) ||
                    (!state.keepOnline && !state.overlayEnabled) ||
                    (state.status == ConnectionStatus.Error && waitForConnection)
                ) {
                    stopSelf()
                    return@collectLatest
                }

                NotificationCenter.updatePresence(this@PresenceService, state)
                updateOverlay(state)
            }
        }
    }

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onDestroy() {
        collectJob?.cancel()
        scope.cancel()
        removeOverlay()
        super.onDestroy()
    }

    private fun updateOverlay(state: RoomUiState) {
        val canOverlay = Settings.canDrawOverlays(this)
        if (!state.overlayEnabled ||
            !canOverlay ||
            state.status != ConnectionStatus.Connected
        ) {
            removeOverlay()
            return
        }

        val text = buildString {
            append(state.roomName.ifBlank { "9mRoom" })
            append(" · ")
            append(state.onlineCount)
            append(" 人")
            append(" · ")
            append(state.self?.modeLabel ?: modeLabel(state.selfMode))
        }

        if (overlayView == null) {
            val view = TextView(this).apply {
                setTextColor(Color.WHITE)
                textSize = 12f
                setPadding(28, 14, 28, 14)
                background = GradientDrawable().apply {
                    cornerRadius = 28f
                    setColor(Color.parseColor("#E6231917"))
                    setStroke(1, Color.parseColor("#55FFFFFF"))
                }
                elevation = 8f
                isClickable = true
                setOnClickListener {
                    startActivity(
                        android.content.Intent(this@PresenceService, com.ninemroom.app.MainActivity::class.java)
                            .addFlags(
                                android.content.Intent.FLAG_ACTIVITY_NEW_TASK or
                                    android.content.Intent.FLAG_ACTIVITY_SINGLE_TOP,
                            ),
                    )
                }
                setOnTouchListener(DragTouchListener())
            }
            val params = WindowManager.LayoutParams(
                WindowManager.LayoutParams.WRAP_CONTENT,
                WindowManager.LayoutParams.WRAP_CONTENT,
                WindowManager.LayoutParams.TYPE_APPLICATION_OVERLAY,
                WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE or
                    WindowManager.LayoutParams.FLAG_NOT_TOUCH_MODAL,
                PixelFormat.TRANSLUCENT,
            ).apply {
                gravity = Gravity.TOP or Gravity.END
                x = 24
                y = 300
            }
            try {
                getSystemService(WindowManager::class.java).addView(view, params)
                overlayView = view
                overlayParams = params
            } catch (_: Exception) {
                removeOverlay()
                return
            }
        }

        overlayView?.text = text
    }

    private fun removeOverlay() {
        val view = overlayView ?: return
        try {
            getSystemService(WindowManager::class.java).removeView(view)
        } catch (_: Exception) {
            // 已经移除
        }
        overlayView = null
        overlayParams = null
    }

    private inner class DragTouchListener : View.OnTouchListener {
        private var downRawX = 0f
        private var downRawY = 0f
        private var startX = 0
        private var startY = 0

        override fun onTouch(view: View, event: MotionEvent): Boolean {
            val params = overlayParams ?: return false
            when (event.action) {
                MotionEvent.ACTION_DOWN -> {
                    downRawX = event.rawX
                    downRawY = event.rawY
                    startX = params.x
                    startY = params.y
                    return true
                }

                MotionEvent.ACTION_MOVE -> {
                    params.x = (startX - (event.rawX - downRawX).toInt()).coerceAtLeast(0)
                    params.y = (startY + (event.rawY - downRawY).toInt()).coerceAtLeast(0)
                    try {
                        getSystemService(WindowManager::class.java).updateViewLayout(view, params)
                    } catch (_: Exception) {
                        // 窗口可能已被移除
                    }
                    return true
                }

                MotionEvent.ACTION_UP -> {
                    view.performClick()
                    return true
                }
            }
            return false
        }
    }

    companion object {
        const val EXTRA_WAIT_FOR_CONNECTION = "wait_for_connection"

        fun intent(context: Context): Intent = Intent(context, PresenceService::class.java)
    }
}
