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

enum class FocusPhase {
    FOCUS,
    BREAK,
}

data class FocusState(
    val phase: FocusPhase = FocusPhase.FOCUS,
    val focusDurationSeconds: Int = 25 * 60,
    val breakDurationSeconds: Int = 5 * 60,
    val durationSeconds: Int = 25 * 60,
    val remainingSeconds: Int = 25 * 60,
    val running: Boolean = false,
    val startedAt: Long = 0,
    val completedTodaySeconds: Long = 0,
    val completedRounds: Int = 0,
    val cycleEnabled: Boolean = false,
) {
    val progress: Float
        get() = if (durationSeconds <= 0) 0f else remainingSeconds / durationSeconds.toFloat()

    val display: String
        get() = "%02d:%02d".format(remainingSeconds / 60, remainingSeconds % 60)

    val isBreak: Boolean
        get() = phase == FocusPhase.BREAK

    val phaseName: String
        get() = if (isBreak) "休息" else "专注"
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
        val current = _state.value
        when {
            current.running && current.remainingSeconds <= 0 -> {
                if (completeCurrent()) startTicker()
            }
            current.running -> startTicker()
        }
    }

    fun startCurrent() {
        if (_state.value.isBreak) {
            startBreak(_state.value.breakDurationSeconds / 60)
        } else {
            startFocus(_state.value.focusDurationSeconds / 60)
        }
    }

    fun startFocus(minutes: Int = _state.value.focusDurationSeconds / 60) {
        val duration = minutes.coerceIn(1, 180) * 60
        _state.value = _state.value.copy(
            phase = FocusPhase.FOCUS,
            focusDurationSeconds = duration,
            durationSeconds = duration,
            remainingSeconds = duration,
            running = true,
            startedAt = System.currentTimeMillis(),
        )
        persist()
        startTicker()
    }

    fun startBreak(minutes: Int = _state.value.breakDurationSeconds / 60) {
        val duration = minutes.coerceIn(1, 60) * 60
        _state.value = _state.value.copy(
            phase = FocusPhase.BREAK,
            breakDurationSeconds = duration,
            durationSeconds = duration,
            remainingSeconds = duration,
            running = true,
            startedAt = System.currentTimeMillis(),
        )
        persist()
        startTicker()
    }

    fun setPhase(phase: FocusPhase) {
        if (_state.value.phase == phase) return
        ticker?.cancel()
        ticker = null
        val duration = if (phase == FocusPhase.BREAK) {
            _state.value.breakDurationSeconds
        } else {
            _state.value.focusDurationSeconds
        }
        _state.value = _state.value.copy(
            phase = phase,
            durationSeconds = duration,
            remainingSeconds = duration,
            running = false,
            startedAt = 0,
        )
        persist()
    }

    fun setDuration(minutes: Int) {
        ticker?.cancel()
        ticker = null
        val duration = if (_state.value.isBreak) {
            minutes.coerceIn(1, 60) * 60
        } else {
            minutes.coerceIn(1, 180) * 60
        }
        _state.value = if (_state.value.isBreak) {
            _state.value.copy(
                breakDurationSeconds = duration,
                durationSeconds = duration,
                remainingSeconds = duration,
                running = false,
                startedAt = 0,
            )
        } else {
            _state.value.copy(
                focusDurationSeconds = duration,
                durationSeconds = duration,
                remainingSeconds = duration,
                running = false,
                startedAt = 0,
            )
        }
        persist()
    }

    fun applySettings(focusMinutes: Int, breakMinutes: Int, cycleEnabled: Boolean) {
        ticker?.cancel()
        ticker = null
        val focusDuration = focusMinutes.coerceIn(1, 180) * 60
        val breakDuration = breakMinutes.coerceIn(1, 60) * 60
        val duration = if (_state.value.isBreak) breakDuration else focusDuration
        _state.value = _state.value.copy(
            focusDurationSeconds = focusDuration,
            breakDurationSeconds = breakDuration,
            cycleEnabled = cycleEnabled,
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
        if (state.isBreak) return state.completedTodaySeconds
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
                    if (!completeCurrent()) break
                } else {
                    _state.value = _state.value.copy(remainingSeconds = remaining)
                }
            }
        }
    }

    private fun completeCurrent(): Boolean {
        val current = _state.value
        var continueCycle = false
        if (current.phase == FocusPhase.FOCUS) {
            continueCycle = current.cycleEnabled
            val next = current.copy(
                phase = FocusPhase.BREAK,
                durationSeconds = current.breakDurationSeconds,
                remainingSeconds = current.breakDurationSeconds,
                running = continueCycle,
                startedAt = if (continueCycle) System.currentTimeMillis() else 0,
                completedTodaySeconds = current.completedTodaySeconds + current.focusDurationSeconds,
                completedRounds = current.completedRounds + 1,
            )
            _state.value = next
            if (settings.notifyFocusDone) {
                NotificationCenter.focusDone(
                    context,
                    if (continueCycle) {
                        "这一轮完成了，已开始 ${current.breakDurationSeconds / 60} 分钟休息。"
                    } else {
                        "这一轮完成了，起来活动一下。"
                    },
                )
            }
        } else {
            continueCycle = current.cycleEnabled
            val next = current.copy(
                phase = FocusPhase.FOCUS,
                durationSeconds = current.focusDurationSeconds,
                remainingSeconds = current.focusDurationSeconds,
                running = continueCycle,
                startedAt = if (continueCycle) System.currentTimeMillis() else 0,
            )
            _state.value = next
            if (settings.notifyFocusDone) {
                NotificationCenter.breakDone(
                    context,
                    if (continueCycle) "休息结束，已开始下一轮专注。" else "准备好开始下一轮专注了吗？",
                )
            }
        }
        persist()
        return continueCycle
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
        val storedDuration = preferences.getInt("duration", 25 * 60)
        val focusDuration = preferences.getInt("focus_duration", storedDuration)
            .coerceIn(60, 180 * 60)
        val breakDuration = preferences.getInt("break_duration", 5 * 60)
            .coerceIn(60, 60 * 60)
        val phase = runCatching {
            FocusPhase.valueOf(preferences.getString("phase", FocusPhase.FOCUS.name).orEmpty())
        }.getOrDefault(FocusPhase.FOCUS)
        val duration = if (phase == FocusPhase.BREAK) breakDuration else focusDuration
        val running = preferences.getBoolean("running", false)
        val startedAt = preferences.getLong("started_at", 0)
        val pausedRemaining = preferences.getInt("remaining", duration)
        val completedRounds = preferences.getInt("completed_rounds", 0)
        val cycleEnabled = preferences.getBoolean("cycle_enabled", false)
        if (running && startedAt > 0) {
            val elapsed = ((System.currentTimeMillis() - startedAt) / 1000).toInt()
            val remaining = (duration - elapsed).coerceAtLeast(0)
            return FocusState(
                phase = phase,
                focusDurationSeconds = focusDuration,
                breakDurationSeconds = breakDuration,
                durationSeconds = duration,
                remainingSeconds = remaining,
                running = true,
                startedAt = startedAt,
                completedTodaySeconds = completed,
                completedRounds = completedRounds,
                cycleEnabled = cycleEnabled,
            )
        }
        return FocusState(
            phase = phase,
            focusDurationSeconds = focusDuration,
            breakDurationSeconds = breakDuration,
            durationSeconds = duration,
            remainingSeconds = pausedRemaining.coerceIn(0, duration),
            running = false,
            startedAt = 0,
            completedTodaySeconds = completed,
            completedRounds = completedRounds,
            cycleEnabled = cycleEnabled,
        )
    }

    private fun persist() {
        val state = _state.value
        preferences.edit()
            .putString("phase", state.phase.name)
            .putInt("focus_duration", state.focusDurationSeconds)
            .putInt("break_duration", state.breakDurationSeconds)
            .putInt("duration", state.durationSeconds)
            .putInt("remaining", state.remainingSeconds)
            .putBoolean("running", state.running)
            .putLong("started_at", state.startedAt)
            .putLong("completed_today", state.completedTodaySeconds)
            .putInt("completed_rounds", state.completedRounds)
            .putBoolean("cycle_enabled", state.cycleEnabled)
            .putString("day", LocalDate.now().toString())
            .apply()
    }
}
