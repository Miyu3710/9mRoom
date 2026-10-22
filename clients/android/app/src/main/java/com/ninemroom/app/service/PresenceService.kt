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
import android.text.TextUtils
import android.util.TypedValue
import android.view.Gravity
import android.view.MotionEvent
import android.view.View
import android.view.WindowManager
import android.widget.LinearLayout
import android.widget.ScrollView
import android.widget.TextView
import androidx.core.app.ServiceCompat
import com.ninemroom.app.NineRoomApplication
import com.ninemroom.app.RoomUiState
import com.ninemroom.app.data.ConnectionStatus
import com.ninemroom.app.data.RoomMember
import com.ninemroom.app.data.modeLabel
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.flow.collectLatest
import kotlinx.coroutines.launch
import kotlin.math.min

class PresenceService : Service() {
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main.immediate)
    private var collectJob: Job? = null
    private var overlayView: TextView? = null
    private var overlayParams: WindowManager.LayoutParams? = null
    private var panelView: View? = null
    private var panelParams: WindowManager.LayoutParams? = null
    private var panelExpanded = false
    private var lastState: RoomUiState? = null
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
        updateOverlay((application as NineRoomApplication).roomController.state.value)
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
        lastState = state
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
                setTextSize(TypedValue.COMPLEX_UNIT_SP, 12f)
                setPadding(dp(28), dp(14), dp(28), dp(14))
                background = GradientDrawable().apply {
                    cornerRadius = dp(28).toFloat()
                    setColor(Color.parseColor("#E6231917"))
                    setStroke(1, Color.parseColor("#55FFFFFF"))
                }
                elevation = dp(8).toFloat()
                isClickable = true
                setOnClickListener { togglePanel() }
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
                x = dp(16)
                y = dp(120)
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
        if (panelExpanded) {
            updatePanel(state)
        }
    }

    private fun togglePanel() {
        val state = lastState ?: return
        if (panelExpanded) {
            removePanel()
            return
        }
        panelExpanded = true
        createPanel(state)
    }

    private fun createPanel(state: RoomUiState) {
        removePanel()
        val panel = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(dp(10), dp(8), dp(10), dp(8))
            background = GradientDrawable().apply {
                cornerRadius = dp(18).toFloat()
                setColor(Color.parseColor("#F2231917"))
                setStroke(1, Color.parseColor("#55FFFFFF"))
            }
            elevation = dp(8).toFloat()
        }
        val list = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
        }
        val scroll = ScrollView(this).apply {
            isFillViewport = false
            addView(list)
        }
        panel.addView(scroll)
        fillMemberList(list, state)

        val width = dp(230)
        val panelHeight = dp(min(maxOf(state.members.size, 1), 5) * 34 + 16)
        val chipY = overlayParams?.y ?: dp(120)
        val chipHeight = overlayView?.height?.takeIf { it > 0 } ?: dp(36)
        val params = WindowManager.LayoutParams(
            width,
            panelHeight,
            WindowManager.LayoutParams.TYPE_APPLICATION_OVERLAY,
            WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE or
                WindowManager.LayoutParams.FLAG_NOT_TOUCH_MODAL,
            PixelFormat.TRANSLUCENT,
        ).apply {
            gravity = Gravity.TOP or Gravity.END
            x = dp(16)
            y = chipY + chipHeight + dp(8)
        }

        try {
            getSystemService(WindowManager::class.java).addView(panel, params)
            panelView = panel
            panelParams = params
        } catch (_: Exception) {
            panelExpanded = false
            removePanel()
        }
    }

    private fun updatePanel(state: RoomUiState) {
        val panel = panelView as? LinearLayout ?: return
        if (panel.childCount == 0) return
        val scroll = panel.getChildAt(0) as? ScrollView ?: return
        val list = scroll.getChildAt(0) as? LinearLayout ?: return
        fillMemberList(list, state)
        panelParams?.let { params ->
            params.height = dp(min(maxOf(state.members.size, 1), 5) * 34 + 16)
            try {
                getSystemService(WindowManager::class.java).updateViewLayout(panel, params)
            } catch (_: Exception) {
                removePanel()
            }
        }
    }

    private fun fillMemberList(list: LinearLayout, state: RoomUiState) {
        list.removeAllViews()
        if (state.members.isEmpty()) {
            list.addView(memberText("等待成员同步…", false))
            return
        }
        state.members.take(20).forEach { member ->
            list.addView(memberRow(member, member.id == state.selfId))
        }
    }

    private fun memberRow(member: RoomMember, isSelf: Boolean): View {
        val row = TextView(this).apply {
            text = buildString {
                append(if (isSelf) "${member.name}（我）" else member.name)
                append("    ")
                append(if (member.online) member.modeLabel else "离线")
            }
            setTextColor(if (member.online) Color.WHITE else Color.parseColor("#99FFFFFF"))
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 12f)
            setPadding(dp(8), dp(7), dp(8), dp(7))
            maxLines = 1
            ellipsize = TextUtils.TruncateAt.END
        }
        return row
    }

    private fun memberText(text: String, isSelf: Boolean): TextView = TextView(this).apply {
        this.text = text
        setTextColor(if (isSelf) Color.WHITE else Color.parseColor("#CCFFFFFF"))
        setTextSize(TypedValue.COMPLEX_UNIT_SP, 11f)
        setPadding(dp(8), dp(6), dp(8), dp(6))
    }

    private fun removePanel() {
        panelExpanded = false
        val view = panelView ?: return
        try {
            getSystemService(WindowManager::class.java).removeView(view)
        } catch (_: Exception) {
            // 已经移除
        }
        panelView = null
        panelParams = null
    }

    private fun removeOverlay() {
        removePanel()
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
        private var moved = false

        override fun onTouch(view: View, event: MotionEvent): Boolean {
            val params = overlayParams ?: return false
            when (event.action) {
                MotionEvent.ACTION_DOWN -> {
                    downRawX = event.rawX
                    downRawY = event.rawY
                    startX = params.x
                    startY = params.y
                    moved = false
                    return true
                }

                MotionEvent.ACTION_MOVE -> {
                    val dx = event.rawX - downRawX
                    val dy = event.rawY - downRawY
                    if (kotlin.math.abs(dx) > dp(4) || kotlin.math.abs(dy) > dp(4)) {
                        moved = true
                        removePanel()
                    }
                    params.x = (startX - dx.toInt()).coerceAtLeast(0)
                    params.y = (startY + dy.toInt()).coerceAtLeast(0)
                    try {
                        getSystemService(WindowManager::class.java).updateViewLayout(overlayView, params)
                    } catch (_: Exception) {
                        // 窗口可能已被移除
                    }
                    return true
                }

                MotionEvent.ACTION_UP -> {
                    if (!moved) {
                        view.performClick()
                    }
                    return true
                }
            }
            return false
        }
    }

    private fun dp(value: Int): Int =
        (value * resources.displayMetrics.density).toInt()

    companion object {
        const val EXTRA_WAIT_FOR_CONNECTION = "wait_for_connection"

        fun intent(context: Context): Intent = Intent(context, PresenceService::class.java)
    }
}
