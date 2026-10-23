package com.ninemroom.app.data

import android.content.Context
import com.ninemroom.app.service.NotificationCenter
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import java.time.LocalDate

data class FocusState(
    val durationSeconds: Int = 25 * 60,
    val remainingSeconds: Int = 25 * 60,
    val running: Boolean = false,
    val startedAt: Long = 0,
    val completedTodaySeconds: Long = 0,
) {
    val progress: Float
        get() = if (durationSeconds <= 0) 0f else remainingSeconds / durationSeconds.toFloat()

    val display: String
        get() = "%02d:%02d".format(remainingSeconds / 60, remainingSeconds % 60)
}

class FocusTimerController(private val context: Context) {
    private val preferences = context.applicationContext
        .getSharedPreferences("nine_room_focus", Context.MODE_PRIVATE)
    private val settings = AppSettings(context)
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main.immediate)
    private val _state = MutableStateFlow(load())
    val state: StateFlow<FocusState> = _state.asStateFlow()
    private var ticker: Job? = null

    init {
        if (_state.value.running && _state.value.remainingSeconds > 0) {
            startTicker()
        }
    }

    fun start(minutes: Int = 25) {
        val duration = (minutes.coerceIn(1, 180) * 60)
        _state.value = _state.value.copy(
            durationSeconds = duration,
            remainingSeconds = duration,
            running = true,
            startedAt = System.currentTimeMillis(),
        )
        persist()
        startTicker()
    }

    fun setDuration(minutes: Int) {
        ticker?.cancel()
        ticker = null
        val duration = (minutes.coerceIn(1, 180) * 60)
        _state.value = _state.value.copy(
            durationSeconds = duration,
            remainingSeconds = duration,
            running = false,
            startedAt = 0,
        )
        persist()
    }

    fun pause() {
        if (!_state.value.running) return
        ticker?.cancel()
        ticker = null
        _state.value = _state.value.copy(
            running = false,
            remainingSeconds = currentRemaining(),
            startedAt = 0,
        )
        persist()
    }

    fun resume() {
        if (_state.value.running || _state.value.remainingSeconds <= 0) return
        val elapsed = _state.value.durationSeconds - _state.value.remainingSeconds
        _state.value = _state.value.copy(
            running = true,
            startedAt = System.currentTimeMillis() - elapsed * 1000L,
        )
        persist()
        startTicker()
    }

    fun reset() {
        ticker?.cancel()
        ticker = null
        _state.value = _state.value.copy(
            remainingSeconds = _state.value.durationSeconds,
            running = false,
            startedAt = 0,
        )
        persist()
    }

    fun focusSecondsToday(): Long {
        val state = _state.value
        val elapsed = if (state.running) {
            (state.durationSeconds - currentRemaining()).coerceAtLeast(0)
        } else {
            (state.durationSeconds - state.remainingSeconds).coerceAtLeast(0)
        }
        return state.completedTodaySeconds + elapsed
    }

    private fun startTicker() {
        ticker?.cancel()
        ticker = scope.launch {
            while (isActive) {
                delay(1000)
                val remaining = currentRemaining()
                if (remaining <= 0) {
                    val duration = _state.value.durationSeconds
                    _state.value = _state.value.copy(
                        running = false,
                        remainingSeconds = 0,
                        startedAt = 0,
                        completedTodaySeconds = _state.value.completedTodaySeconds + duration,
                    )
                    persist()
                    if (settings.notifyFocusDone) {
                        NotificationCenter.focusDone(context)
                    }
                    break
                }
                _state.value = _state.value.copy(remainingSeconds = remaining)
            }
        }
    }

    private fun currentRemaining(): Int {
        val state = _state.value
        if (!state.running || state.startedAt <= 0) return state.remainingSeconds
        val elapsed = ((System.currentTimeMillis() - state.startedAt) / 1000).toInt()
        return (state.durationSeconds - elapsed).coerceAtLeast(0)
    }

    private fun load(): FocusState {
        val today = LocalDate.now().toString()
        val storedDay = preferences.getString("day", today).orEmpty()
        val completed = if (storedDay == today) {
            preferences.getLong("completed_today", 0)
        } else {
            preferences.edit().putString("day", today).putLong("completed_today", 0).apply()
            0
        }
        val duration = preferences.getInt("duration", 25 * 60)
        val running = preferences.getBoolean("running", false)
        val startedAt = preferences.getLong("started_at", 0)
        val pausedRemaining = preferences.getInt("remaining", duration)
        if (running && startedAt > 0) {
            val elapsed = ((System.currentTimeMillis() - startedAt) / 1000).toInt()
            val remaining = (duration - elapsed).coerceAtLeast(0)
            return FocusState(duration, remaining, remaining > 0, startedAt, completed)
        }
        return FocusState(duration, pausedRemaining.coerceIn(0, duration), false, 0, completed)
    }

    private fun persist() {
        val state = _state.value
        preferences.edit()
            .putInt("duration", state.durationSeconds)
            .putInt("remaining", state.remainingSeconds)
            .putBoolean("running", state.running)
            .putLong("started_at", state.startedAt)
            .putLong("completed_today", state.completedTodaySeconds)
            .putString("day", LocalDate.now().toString())
            .apply()
    }
}
