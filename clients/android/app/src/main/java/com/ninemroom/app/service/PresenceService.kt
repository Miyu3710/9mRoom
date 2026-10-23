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
import android.view.View.MeasureSpec
import android.view.WindowManager
import android.widget.LinearLayout
import android.widget.ScrollView
import android.widget.TextView
import androidx.core.app.ServiceCompat
import com.ninemroom.app.NineRoomApplication
import com.ninemroom.app.RoomUiState
import com.ninemroom.app.data.AppSettings
import com.ninemroom.app.data.ConnectionStatus
import com.ninemroom.app.data.RoomMember
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.flow.collectLatest
import kotlinx.coroutines.launch

private class MaxHeightScrollView(
    context: Context,
    private val maxHeightPx: Int,
) : ScrollView(context) {
    override fun onMeasure(widthMeasureSpec: Int, heightMeasureSpec: Int) {
        val cappedHeight = MeasureSpec.makeMeasureSpec(maxHeightPx, MeasureSpec.AT_MOST)
        super.onMeasure(widthMeasureSpec, cappedHeight)
    }
}

class PresenceService : Service() {
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main.immediate)
    private val settings by lazy { AppSettings(this) }
    private var collectJob: Job? = null
    private var overlayView: TextView? = null
    private var overlayParams: WindowManager.LayoutParams? = null
    private var panelView: View? = null
    private var panelParams: WindowManager.LayoutParams? = null
    private var panelList: LinearLayout? = null
    private var panelAttached = false
    private var panelExpanded = false
    private var lastState: RoomUiState? = null
    private var waitForConnection = false
    private var appliedMargin = Int.MIN_VALUE
    private var appliedSize = -1

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
        saveOverlayPosition()
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

        if (overlayView == null && !createChip()) {
            return
        }
        renderChip(state)
        if (panelExpanded) {
            renderPanel(state)
        }
    }

    private fun createChip(): Boolean {
        val view = TextView(this).apply {
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
        val params = WindowManager.LayoutParams(
            dp(120),
            dp(30),
            WindowManager.LayoutParams.TYPE_APPLICATION_OVERLAY,
            WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE or
                WindowManager.LayoutParams.FLAG_NOT_TOUCH_MODAL,
            PixelFormat.TRANSLUCENT,
        ).apply {
            val screenWidth = resources.displayMetrics.widthPixels
            val screenHeight = resources.displayMetrics.heightPixels
            val size = dp(settings.overlaySize)
            val width = size
            val height = dp(30)
            val maxX = (screenWidth - width).coerceAtLeast(1)
            val maxY = maxOverlayY(height)
            gravity = Gravity.TOP or Gravity.START
            x = if (settings.overlayXFraction >= 0f) {
                (settings.overlayXFraction * maxX).toInt().coerceIn(0, maxX)
            } else {
                screenWidth - width - dp(12)
            }
            y = if (settings.overlayYFraction >= 0f) {
                (settings.overlayYFraction * maxY).toInt().coerceIn(0, maxY)
            } else {
                dp(108)
            }
            this.width = width
            this.height = height
        }
        return try {
            getSystemService(WindowManager::class.java).addView(view, params)
            overlayView = view
            overlayParams = params
            true
        } catch (_: Exception) {
            removeOverlay()
            false
        }
    }

    private fun renderChip(state: RoomUiState) {
        val view = overlayView ?: return
        val params = overlayParams ?: return
        view.text = "${state.roomName.ifBlank { "9mRoom" }} · ${state.onlineCount} 人"
        view.alpha = state.overlayOpacity
        if (appliedSize != state.overlaySize) {
            appliedSize = state.overlaySize
            val screenWidth = resources.displayMetrics.widthPixels
            val screenHeight = resources.displayMetrics.heightPixels
            params.width = dp(state.overlaySize)
            params.height = dp(30)
            params.x = params.x.coerceIn(
                0,
                (screenWidth - dp(state.overlaySize)).coerceAtLeast(0),
            )
            params.y = params.y.coerceIn(0, maxOverlayY(dp(30)))
            try {
                getSystemService(WindowManager::class.java).updateViewLayout(view, params)
            } catch (_: Exception) {
                // 窗口可能已被移除
            }
        }
        if (appliedMargin != state.overlayMargin) {
            appliedMargin = state.overlayMargin
            view.setPadding(
                dp(state.overlayMargin),
                dp(6),
                dp(state.overlayMargin),
                dp(6),
            )
        }
    }

    private fun togglePanel() {
        val state = lastState ?: return
        if (panelExpanded) {
            hidePanel()
        } else {
            showPanel(state)
        }
    }

    private fun showPanel(state: RoomUiState) {
        if (panelExpanded) return
        overlayView ?: return
        val chipParams = overlayParams ?: return
        val panel = panelView ?: createPanelView(state).also { created ->
            panelList = created.findViewWithTag("member-list")
            panelView = created
        }
        val list = panelList ?: return
        fillMemberList(list, state)
        panel.visibility = View.VISIBLE
        panel.alpha = 0f
        val panelHeight = measurePanel(panel)
        val position = panelPosition(chipParams, panelHeight)
        val params = panelParams ?: WindowManager.LayoutParams(
            dp(250),
            WindowManager.LayoutParams.WRAP_CONTENT,
            WindowManager.LayoutParams.TYPE_APPLICATION_OVERLAY,
            WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE or
                WindowManager.LayoutParams.FLAG_NOT_TOUCH_MODAL,
            PixelFormat.TRANSLUCENT,
        ).apply {
            gravity = Gravity.TOP or Gravity.START
        }
        params.width = dp(250)
        params.height = WindowManager.LayoutParams.WRAP_CONTENT
        params.x = position.first
        params.y = position.second
        panelParams = params
        if (!panelAttached) {
            try {
                getSystemService(WindowManager::class.java).addView(panel, params)
                panelAttached = true
            } catch (_: Exception) {
                panelView = null
                panelParams = null
                panelList = null
                panelAttached = false
                return
            }
        } else {
            setPanelTouchable(true)
            try {
                getSystemService(WindowManager::class.java).updateViewLayout(panel, params)
            } catch (_: Exception) {
                return
            }
        }
        setPanelTouchable(true)
        panel.translationY = -dp(6).toFloat()
        panelExpanded = true
        panel.animate()
            .alpha(state.overlayOpacity)
            .translationY(0f)
            .setDuration(180)
            .start()
    }

    private fun hidePanel() {
        val panel = panelView ?: return
        panelExpanded = false
        panel.animate()
            .alpha(0f)
            .translationY(-dp(6).toFloat())
            .setDuration(150)
            .withEndAction {
                if (!panelExpanded) {
                    panel.alpha = 0f
                    panel.visibility = View.GONE
                    setPanelTouchable(false)
                    collapsePanelWindow()
                }
            }
            .start()
    }

    private fun measurePanel(panel: View): Int {
        panel.measure(
            View.MeasureSpec.makeMeasureSpec(dp(250), View.MeasureSpec.EXACTLY),
            View.MeasureSpec.makeMeasureSpec(dp(300), View.MeasureSpec.AT_MOST),
        )
        return panel.measuredHeight.coerceAtLeast(dp(40))
    }

    private fun panelPosition(
        chipParams: WindowManager.LayoutParams,
        panelHeight: Int,
    ): Pair<Int, Int> {
        val width = dp(250)
        val screenWidth = resources.displayMetrics.widthPixels
        val screenHeight = resources.displayMetrics.heightPixels
        val x = chipParams.x
            .coerceAtMost(screenWidth - width - dp(4))
            .coerceAtLeast(0)
        val chipBottom = chipParams.y + (overlayView?.height ?: dp(30))
        val fitsBelow = chipBottom + dp(6) + panelHeight <= screenHeight - dp(8)
        val gap = if (fitsBelow) dp(6) else dp(20)
        val below = chipBottom + gap
        val above = chipParams.y - panelHeight - gap
        val y = if (fitsBelow) {
            below
        } else if (above >= dp(8)) {
            above
        } else {
            below
        }
        return x to y
    }

    private fun collapsePanelWindow() {
        val panel = panelView ?: return
        val params = panelParams ?: return
        params.width = 1
        params.height = 1
        params.x = 0
        params.y = 0
        try {
            getSystemService(WindowManager::class.java).updateViewLayout(panel, params)
        } catch (_: Exception) {
            // 窗口可能已被移除
        }
    }

    private fun setPanelTouchable(touchable: Boolean) {
        val panel = panelView ?: return
        val params = panelParams ?: return
        params.flags = WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE or
            WindowManager.LayoutParams.FLAG_NOT_TOUCH_MODAL or
            if (touchable) 0 else WindowManager.LayoutParams.FLAG_NOT_TOUCHABLE
        try {
            getSystemService(WindowManager::class.java).updateViewLayout(panel, params)
        } catch (_: Exception) {
            // 窗口可能已被移除
        }
    }

    private fun createPanelView(state: RoomUiState): View {
        val panel = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(dp(8), dp(6), dp(8), dp(6))
            background = GradientDrawable().apply {
                cornerRadius = dp(12).toFloat()
                setColor(Color.parseColor("#F2231917"))
                setStroke(1, Color.parseColor("#44FFFFFF"))
            }
            elevation = dp(8).toFloat()
        }
        val scroll = MaxHeightScrollView(this, maxHeightPx = dp(300)).apply {
            isFillViewport = false
        }
        val list = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            tag = "member-list"
        }
        scroll.addView(list)
        panel.addView(scroll)
        fillMemberList(list, state)
        return panel
    }

    private fun renderPanel(state: RoomUiState) {
        val panel = panelView ?: return
        val list = panelList ?: return
        fillMemberList(list, state)
        val chipParams = overlayParams ?: return
        val params = panelParams ?: return
        val panelHeight = measurePanel(panel)
        val position = panelPosition(chipParams, panelHeight)
        params.width = dp(250)
        params.height = WindowManager.LayoutParams.WRAP_CONTENT
        params.x = position.first
        params.y = position.second
        panel.requestLayout()
        try {
            getSystemService(WindowManager::class.java).updateViewLayout(panel, params)
        } catch (_: Exception) {
            return
        }
        panel.alpha = state.overlayOpacity
    }

    private fun fillMemberList(list: LinearLayout, state: RoomUiState) {
        list.removeAllViews()
        if (state.members.isEmpty()) {
            list.addView(
                TextView(this).apply {
                    text = "等待成员同步…"
                    setTextColor(Color.parseColor("#CCFFFFFF"))
                    setTextSize(TypedValue.COMPLEX_UNIT_SP, 11f)
                    setPadding(dp(6), dp(6), dp(6), dp(6))
                },
            )
            return
        }
        state.members.take(20).forEach { member ->
            list.addView(memberRow(member, member.id == state.selfId))
        }
    }

    private fun memberRow(member: RoomMember, isSelf: Boolean): View {
        val row = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(dp(6), dp(5), dp(6), dp(6))
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
            maxLines = Int.MAX_VALUE
        }
        row.addView(header)
        row.addView(detail)
        return row
    }

    private fun removePanelView() {
        val view = panelView ?: return
        try {
            getSystemService(WindowManager::class.java).removeView(view)
        } catch (_: Exception) {
            // 已经移除
        }
        panelView = null
        panelParams = null
        panelList = null
        panelAttached = false
    }

    private fun removeOverlay() {
        saveOverlayPosition()
        removePanelView()
        panelExpanded = false
        val view = overlayView ?: return
        try {
            getSystemService(WindowManager::class.java).removeView(view)
        } catch (_: Exception) {
            // 已经移除
        }
        overlayView = null
        overlayParams = null
        appliedMargin = Int.MIN_VALUE
        appliedSize = -1
    }

    private fun saveOverlayPosition() {
        val view = overlayView ?: return
        val params = overlayParams ?: return
        val maxX = (resources.displayMetrics.widthPixels - view.width).coerceAtLeast(1)
        val maxY = maxOverlayY(view.height)
        settings.overlayXFraction = params.x.toFloat() / maxX
        settings.overlayYFraction = params.y.toFloat() / maxY
    }

    private fun maxOverlayY(chipHeight: Int): Int {
        val screenHeight = resources.displayMetrics.heightPixels
        val navBarHeight = runCatching {
            val id = resources.getIdentifier("navigation_bar_height", "dimen", "android")
            if (id > 0) resources.getDimensionPixelSize(id) else 0
        }.getOrDefault(0)
        val reservedBottom = maxOf(navBarHeight, dp(96))
        return (screenHeight - reservedBottom - chipHeight - dp(4)).coerceAtLeast(1)
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
                        if (panelExpanded) hidePanel()
                    }
                    params.x = (startX + dx.toInt()).coerceAtLeast(0)
                    params.y = (startY + dy.toInt())
                        .coerceIn(0, maxOverlayY(overlayView?.height ?: dp(30)))
                    try {
                        getSystemService(WindowManager::class.java).updateViewLayout(overlayView, params)
                    } catch (_: Exception) {
                        // 窗口可能已被移除
                    }
                    return true
                }

                MotionEvent.ACTION_UP -> {
                    if (moved) {
                        saveOverlayPosition()
                    } else {
                        view.performClick()
                    }
                    return true
                }

                MotionEvent.ACTION_CANCEL -> {
                    saveOverlayPosition()
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
