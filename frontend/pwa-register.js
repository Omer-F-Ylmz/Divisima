/*
 * PWA kaydı + push kurulumu (pwa-register.js)
 * index.html'e <script src="/pwa-register.js"></script> ile eklenir (api-client.js'ten SONRA).
 * Service worker'ı kaydeder; kullanıcı giriş yaptıysa bildirim izni ister ve FCM token'ı backend'e yollar.
 */
(function () {
  "use strict";
  if (!("serviceWorker" in navigator)) return;

  window.addEventListener("load", async () => {
    try {
      const reg = await navigator.serviceWorker.register("/service-worker.js");
      console.log("Divisima SW kayıtlı:", reg.scope);
    } catch (e) { console.warn("SW kaydı başarısız:", e); }
  });

  // Açıklama: Giriş sonrası çağrılır - bildirim izni + FCM token kaydı
  // Kullanım: window.DivisimaPush.enable(apiInstance, firebaseMessaging)
  window.DivisimaPush = {
    async enable(api, messaging) {
      try {
        if (!("Notification" in window)) return;
        const perm = await Notification.requestPermission();
        if (perm !== "granted") return;

        // Firebase Messaging ile FCM token al (firebase SDK ayrı yüklenir)
        // messaging: firebase.messaging() örneği; VAPID key gerekir
        if (messaging && messaging.getToken) {
          const token = await messaging.getToken({ vapidKey: window.DIVISIMA_VAPID_KEY });
          if (token && api && api.device) {
            await api.device.register(token, 0); // 0 = Web
            console.log("Push token kaydedildi");
          }
        }
      } catch (e) { console.warn("Push etkinleştirilemedi:", e); }
    }
  };
})();
