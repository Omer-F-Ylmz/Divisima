// Açıklayıcı yorum: Service Worker kaydı - index.html'de <script src="/sw-register.js"></script> ile çağrılır.
// Bu tek satır, web sitesini mobilde ve masaüstünde "kurulabilir uygulama" yapar.
if ('serviceWorker' in navigator) {
  window.addEventListener('load', () => {
    navigator.serviceWorker.register('/service-worker.js')
      .then((reg) => console.log('Divisima SW kayıtlı:', reg.scope))
      .catch((err) => console.warn('SW kaydı başarısız:', err));
  });
}

// Açıklayıcı yorum: "Ana ekrana ekle" istemi (kullanıcıya kurulum önerisi)
let deferredPrompt;
window.addEventListener('beforeinstallprompt', (e) => {
  e.preventDefault();
  deferredPrompt = e;
  // Açıklayıcı yorum: Kendi "Uygulamayı Yükle" butonunu göster (varsa)
  const installBtn = document.getElementById('pwa-install-btn');
  if (installBtn) {
    installBtn.style.display = 'block';
    installBtn.addEventListener('click', async () => {
      installBtn.style.display = 'none';
      deferredPrompt.prompt();
      await deferredPrompt.userChoice;
      deferredPrompt = null;
    });
  }
});
