package com.hustle.filehustle

import android.app.Application
import com.hustle.filehustle.data.DeviceIdentity
import com.hustle.filehustle.net.PeerDiscovery
import com.hustle.filehustle.net.TransferServer

class FileHustleApp : Application() {
    override fun onCreate() {
        super.onCreate()
        DeviceIdentity.init(this)
        PeerDiscovery.init(this)
        TransferServer.init(this)
    }
}
