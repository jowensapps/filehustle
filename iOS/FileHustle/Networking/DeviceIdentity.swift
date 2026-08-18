import Foundation

/// Stable per-install identity used as the mDNS TXT record `id`/`name` — see
/// docs/protocol.md. Not tied to any account.
enum DeviceIdentity {
    private static let idKey = "com.hustle.filehustle.deviceId"
    private static let nameKey = "com.hustle.filehustle.deviceName"

    static var id: String {
        if let existing = UserDefaults.standard.string(forKey: idKey) {
            return existing
        }
        let newId = UUID().uuidString
        UserDefaults.standard.set(newId, forKey: idKey)
        return newId
    }

    /// Defaults to a generated name rather than the real device name (e.g.
    /// "Jared's iPhone") — that broadcasts in the Bonjour TXT record and
    /// nearby-devices list to everyone on the WiFi, not just people you
    /// actually transfer with, on any network (coffee shop, dorm, airport).
    /// Generated once and persisted, same as `id`, so it stays stable
    /// across launches. Still user-editable via the setter for a future
    /// rename affordance.
    static var name: String {
        get {
            if let existing = UserDefaults.standard.string(forKey: nameKey) {
                return existing
            }
            let generated = generateRandomName()
            UserDefaults.standard.set(generated, forKey: nameKey)
            return generated
        }
        set {
            UserDefaults.standard.set(newValue, forKey: nameKey)
        }
    }

    private static func generateRandomName() -> String {
        let adjectives = [
            "Quick", "Silent", "Amber", "Brave", "Calm", "Clever", "Cosmic",
            "Dusty", "Eager", "Fuzzy", "Gentle", "Golden", "Happy", "Hidden",
            "Jolly", "Keen", "Lucky", "Mellow", "Misty", "Nimble", "Plucky",
            "Quiet", "Rapid", "Sleepy", "Sunny", "Swift", "Tidy", "Vivid",
            "Witty", "Zesty",
        ]
        let nouns = [
            "Otter", "Falcon", "Maple", "River", "Comet", "Badger", "Ember",
            "Fox", "Heron", "Ibis", "Jaguar", "Kestrel", "Lynx", "Marlin",
            "Newt", "Orca", "Panda", "Quail", "Raven", "Sparrow", "Toucan",
            "Urchin", "Viper", "Walrus", "Yak", "Zebra", "Pelican", "Osprey",
            "Wombat", "Puffin",
        ]
        return "\(adjectives.randomElement()!) \(nouns.randomElement()!)"
    }
}
