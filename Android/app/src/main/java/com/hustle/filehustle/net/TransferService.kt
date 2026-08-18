package com.hustle.filehustle.net

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.Service
import android.content.Intent
import android.os.Build
import android.os.IBinder
import androidx.core.app.NotificationCompat
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel

/**
 * Thin foreground-service wrapper — Android kills background sockets once
 * the app isn't in the foreground, so the actual listener (TransferServer)
 * and discovery (PeerDiscovery) need a foreground service keeping the
 * process alive. All the actual protocol logic lives in those two
 * singletons, not here.
 */
class TransferService : Service() {
    private val scope = CoroutineScope(SupervisorJob())

    override fun onCreate() {
        super.onCreate()
        startForeground(NOTIFICATION_ID, buildNotification())
        val port = TransferServer.start(scope)
        PeerDiscovery.startAdvertising(port)
        PeerDiscovery.startBrowsing()
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int = START_STICKY

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onDestroy() {
        PeerDiscovery.stop()
        TransferServer.stop()
        scope.cancel()
        super.onDestroy()
    }

    private fun buildNotification(): Notification {
        val channelId = "filehustle_transfer"
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            val channel = NotificationChannel(channelId, "File transfers", NotificationManager.IMPORTANCE_LOW)
            getSystemService(NotificationManager::class.java).createNotificationChannel(channel)
        }
        return NotificationCompat.Builder(this, channelId)
            .setContentTitle("FileHustle")
            .setContentText("Ready to send and receive files on this WiFi network")
            .setSmallIcon(android.R.drawable.stat_sys_upload_done)
            .setOngoing(true)
            .build()
    }

    companion object {
        private const val NOTIFICATION_ID = 1
    }
}
