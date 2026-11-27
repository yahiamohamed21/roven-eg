self.addEventListener('install', function (event) {
    self.skipWaiting();
});

self.addEventListener('activate', function (event) {
    event.waitUntil(self.clients.claim());
});

self.addEventListener('push', function (event) {
    let data = {
        title: 'ROVEN',
        body: 'لديك تنبيه جديد في المتجر',
        url: '/Dashboard',
        tag: 'roven-alert',
        icon: '/images/pwa-icon-192.png',
        badge: '/images/favicon-64.png'
    };

    if (event.data) {
        try {
            const parsed = event.data.json();
            data = Object.assign(data, parsed || {});
        } catch (e) {
            data.body = event.data.text() || data.body;
        }
    }

    const options = {
        body: data.body,
        icon: data.icon || '/images/pwa-icon-192.png',
        badge: data.badge || '/images/favicon-64.png',
        tag: data.tag || 'roven-alert',
        renotify: true,
        dir: 'rtl',
        lang: 'ar',
        data: {
            url: data.url || '/Dashboard'
        }
    };

    event.waitUntil(
        self.registration.showNotification(data.title || 'ROVEN', options)
    );
});

self.addEventListener('notificationclick', function (event) {
    event.notification.close();
    const targetUrl = (event.notification.data && event.notification.data.url) ? event.notification.data.url : '/Dashboard';

    event.waitUntil(
        self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then(function (clientList) {
            for (let i = 0; i < clientList.length; i++) {
                const client = clientList[i];
                if ('focus' in client) {
                    client.focus();
                    if ('navigate' in client) {
                        return client.navigate(targetUrl);
                    }
                    return client;
                }
            }
            if (self.clients.openWindow) {
                return self.clients.openWindow(targetUrl);
            }
        })
    );
});
