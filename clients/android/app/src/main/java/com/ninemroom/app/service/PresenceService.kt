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
    private var overlayRoot: LinearLayout? = null
    private var overlayStatus: TextView? = null
    private var overlayPanel: LinearLayout? = null
    private var overlayList: LinearLayout? = null
    private var overlayScroll: ScrollView? = null
    private var overlayParams: WindowManager.LayoutParams? = null
    private var appliedMargin = Int.MIN_VALUE
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

        if (overlayRoot == null && !createOverlay()) {
            return
        }
        renderOverlay(state)
    }

    private fun createOverlay(): Boolean {
        val root = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            gravity = Gravity.END
        }

        val status = TextView(this).apply {
            setTextColor(Color.WHITE)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 11f)
            gravity = Gravity.CENTER
            setPadding(dp(8), dp(6), dp(8), dp(6))
            background = GradientDrawable().apply {
                cornerRadius = dp(8).toFloat()
                setColor(Color.parseColor("#E6231917"))
                setStroke(1, Color.parseColor("#44FFFFFF"))
            }
            elevation = dp(6).toFloat()
            maxLines = 1
            ellipsize = TextUtils.TruncateAt.END
            isClickable = true
            setOnClickListener { togglePanel() }
            setOnTouchListener(DragTouchListener())
        }

        val panel = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(dp(8), dp(6), dp(8), dp(6))
            background = GradientDrawable().apply {
                cornerRadius = dp(12).toFloat()
                setColor(Color.parseColor("#F2231917"))
                setStroke(1, Color.parseColor("#44FFFFFF"))
            }
            elevation = dp(8).toFloat()
            visibility = View.GONE
        }
        val scroll = ScrollView(this).apply { isFillViewport = false }
        val list = LinearLayout(this).apply { orientation = LinearLayout.VERTICAL }
        scroll.addView(list)
        panel.addView(scroll)
        root.addView(status)
        root.addView(
            panel,
            LinearLayout.LayoutParams(dp(250), LinearLayout.LayoutParams.WRAP_CONTENT).apply {
                topMargin = dp(4)
            },
        )

        val params = WindowManager.LayoutParams(
            WindowManager.LayoutParams.WRAP_CONTENT,
            WindowManager.LayoutParams.WRAP_CONTENT,
            WindowManager.LayoutParams.TYPE_APPLICATION_OVERLAY,
            WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE or
                WindowManager.LayoutParams.FLAG_NOT_TOUCH_MODAL,
            PixelFormat.TRANSLUCENT,
        ).apply {
            gravity = Gravity.TOP or Gravity.END
            x = dp(12)
            y = dp(12)
        }

        return try {
            getSystemService(WindowManager::class.java).addView(root, params)
            overlayRoot = root
            overlayStatus = status
            overlayPanel = panel
            overlayScroll = scroll
            overlayList = list
            overlayParams = params
            true
        } catch (_: Exception) {
            removeOverlay()
            false
        }
    }

    private fun renderOverlay(state: RoomUiState) {
        val status = overlayStatus ?: return
        val root = overlayRoot ?: return
        val panel = overlayPanel ?: return

        val size = dp(state.overlaySize)
        status.layoutParams = status.layoutParams.apply {
            width = size
            height = dp(30)
        }
        status.text = "${state.roomName.ifBlank { "9mRoom" }} · ${state.onlineCount} 人"
        root.alpha = state.overlayOpacity
        if (appliedMargin != state.overlayMargin) {
            appliedMargin = state.overlayMargin
            overlayParams?.let { params ->
                params.x = dp(state.overlayMargin)
                params.y = dp(state.overlayMargin + 96)
                try {
                    getSystemService(WindowManager::class.java).updateViewLayout(root, params)
                } catch (_: Exception) {
                    // 窗口可能已被移除
                }
            }
        }

        if (panelExpanded) {
            panel.visibility = View.VISIBLE
            panel.alpha = 1f
            panel.translationY = 0f
            fillMemberList(state)
        } else if (panel.visibility == View.VISIBLE && panel.alpha == 1f) {
            panel.visibility = View.GONE
        }
    }

    private fun togglePanel() {
        val state = lastState ?: return
        val panel = overlayPanel ?: return
        panelExpanded = !panelExpanded
        if (panelExpanded) {
            fillMemberList(state)
            panel.visibility = View.VISIBLE
            panel.alpha = 0f
            panel.translationY = -dp(6).toFloat()
            panel.animate()
                .alpha(1f)
                .translationY(0f)
                .setDuration(180)
                .start()
        } else {
            panel.animate()
                .alpha(0f)
                .translationY(-dp(6).toFloat())
                .setDuration(150)
                .withEndAction {
                    if (!panelExpanded) panel.visibility = View.GONE
                }
                .start()
        }
    }

    private fun fillMemberList(state: RoomUiState) {
        val list = overlayList ?: return
        val scroll = overlayScroll ?: return
        list.removeAllViews()
        if (state.members.isEmpty()) {
            list.addView(memberText("等待成员同步…"))
        } else {
            state.members.take(20).forEach { member ->
                list.addView(memberRow(member, member.id == state.selfId))
            }
        }
        val rows = min(maxOf(state.members.size, 1), 4)
        scroll.layoutParams = scroll.layoutParams.apply {
            height = dp(rows * 49 + 4)
        }
    }

    private fun memberRow(member: RoomMember, isSelf: Boolean): View {
        val row = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(dp(6), dp(5), dp(6), dp(5))
        }
        val header = TextView(this).apply {
            text = buildString {
                append(if (isSelf) "${member.name}（我）" else member.name)
                append("    ")
                append(if (member.online) member.modeLabel else "离线")
            }
            setTextColor(if (member.online) Color.WHITE else Color.parseColor("#99FFFFFF"))
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 11f)
            maxLines = 1
            ellipsize = TextUtils.TruncateAt.END
        }
        val detail = TextView(this).apply {
            text = member.statusText.ifBlank { "未共享状态" }
            setTextColor(Color.parseColor("#B3FFFFFF"))
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 10f)
            maxLines = 1
            ellipsize = TextUtils.TruncateAt.END
        }
        row.addView(header)
        row.addView(detail)
        return row
    }

    private fun memberText(text: String): TextView = TextView(this).apply {
        this.text = text
        setTextColor(Color.parseColor("#CCFFFFFF"))
        setTextSize(TypedValue.COMPLEX_UNIT_SP, 10f)
        setPadding(dp(6), dp(6), dp(6), dp(6))
    }

    private fun removeOverlay() {
        val view = overlayRoot ?: return
        try {
            getSystemService(WindowManager::class.java).removeView(view)
        } catch (_: Exception) {
            // 已经移除
        }
        overlayRoot = null
        overlayStatus = null
        overlayPanel = null
        overlayList = null
        overlayScroll = null
        overlayParams = null
        appliedMargin = Int.MIN_VALUE
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
                        overlayPanel?.visibility = View.GONE
                        panelExpanded = false
                    }
                    params.x = (startX - dx.toInt()).coerceAtLeast(0)
                    params.y = (startY + dy.toInt()).coerceAtLeast(0)
                    try {
                        getSystemService(WindowManager::class.java).updateViewLayout(overlayRoot, params)
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
