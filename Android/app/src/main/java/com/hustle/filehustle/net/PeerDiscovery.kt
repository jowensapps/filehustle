package com.hustle.filehustle.net

import android.content.Context
import android.net.nsd.NsdManager
import android.net.nsd.NsdServiceInfo
import android.net.wifi.WifiManager
import android.os.Handler
import android.os.Looper
import android.util.Log
import androidx.compose.runtime.mutableStateListOf
import com.hustle.filehustle.data.BonjourInstanceName
import com.hustle.filehustle.data.DeviceIdentity

private const val SERVICE_TYPE = "_filehustle._tcp."
private const val TAG = "PeerDiscovery"

data class Peer(val id: String, val name: String, val host: String, val port: Int)

/**
 * NSD-based mirror of iOS's PeerBrowser + the advertising half of
 * TransferServer. See docs/protocol.md for the wire-level discovery format.
 */
object PeerDiscovery {
    val peers = mutableStateListOf<Peer>()

    private lateinit var appContext: Context
    private val mainHandler = Handler(Looper.getMainLooper())

    private val nsdManager by lazy { appContext.getSystemService(Context.NSD_SERVICE) as NsdManager }
    private val wifiManager by lazy { appContext.getSystemService(Context.WIFI_SERVICE) as WifiManager }

    private var multicastLock: WifiManager.MulticastLock? = null
    private var discoveryListener: NsdManager.DiscoveryListener? = null
    private var registrationListener: NsdManager.RegistrationListener? = null

    fun init(context: Context) {
        appContext = context.applicationContext
    }

    fun startAdvertising(port: Int) {
        val serviceInfo = NsdServiceInfo().apply {
            serviceName = BonjourInstanceName.encode(DeviceIdentity.id, DeviceIdentity.name)
            serviceType = SERVICE_TYPE
            setPort(port)
        }
        val listener = object : NsdManager.RegistrationListener {
            override fun onServiceRegistered(info: NsdServiceInfo) {
                Log.d(TAG, "service registered: ${info.serviceName}")
            }

            override fun onRegistrationFailed(info: NsdServiceInfo, errorCode: Int) {
                Log.e(TAG, "registration failed: $errorCode")
            }

            override fun onServiceUnregistered(info: NsdServiceInfo) {}
            override fun onUnregistrationFailed(info: NsdServiceInfo, errorCode: Int) {}
        }
        nsdManager.registerService(serviceInfo, NsdManager.PROTOCOL_DNS_SD, listener)
        registrationListener = listener
    }

    fun startBrowsing() {
        val lock = wifiManager.createMulticastLock("filehustle-mdns")
        lock.setReferenceCounted(true)
        lock.acquire()
        multicastLock = lock

        val listener = object : NsdManager.DiscoveryListener {
            override fun onDiscoveryStarted(serviceType: String) {
                Log.d(TAG, "discovery started")
            }

            override fun onServiceFound(service: NsdServiceInfo) {
                Log.d(TAG, "found: ${service.serviceName}")
                val (id, _) = BonjourInstanceName.decode(service.serviceName) ?: return
                if (id == DeviceIdentity.id) return
                resolve(service)
            }

            override fun onServiceLost(service: NsdServiceInfo) {
                val (id, _) = BonjourInstanceName.decode(service.serviceName) ?: return
                mainHandler.post { peers.removeAll { it.id == id } }
            }

            override fun onDiscoveryStopped(serviceType: String) {}

            override fun onStartDiscoveryFailed(serviceType: String, errorCode: Int) {
                Log.e(TAG, "start discovery failed: $errorCode")
            }

            override fun onStopDiscoveryFailed(serviceType: String, errorCode: Int) {}
        }
        nsdManager.discoverServices(SERVICE_TYPE, NsdManager.PROTOCOL_DNS_SD, listener)
        discoveryListener = listener
    }

    private fun resolve(service: NsdServiceInfo) {
        nsdManager.resolveService(
            service,
            object : NsdManager.ResolveListener {
                override fun onResolveFailed(info: NsdServiceInfo, errorCode: Int) {
                    Log.e(TAG, "resolve failed for ${info.serviceName}: $errorCode")
                }

                override fun onServiceResolved(info: NsdServiceInfo) {
                    val (id, name) = BonjourInstanceName.decode(info.serviceName) ?: return
                    val host = info.host?.hostAddress ?: return
                    val peer = Peer(id = id, name = name, host = host, port = info.port)
                    mainHandler.post {
                        peers.removeAll { it.id == peer.id }
                        peers.add(peer)
                    }
                }
            }
        )
    }

    fun stop() {
        discoveryListener?.let { runCatching { nsdManager.stopServiceDiscovery(it) } }
        registrationListener?.let { runCatching { nsdManager.unregisterService(it) } }
        multicastLock?.let { if (it.isHeld) it.release() }
        discoveryListener = null
        registrationListener = null
        multicastLock = null
        mainHandler.post { peers.clear() }
    }
}
