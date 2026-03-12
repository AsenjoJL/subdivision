(function () {
    let reservationModulePromise = null;
    let reservationModuleUrl = null;

    function getDashboardModuleInitializers() {
        window.homeownerDashboardModuleInitializers = window.homeownerDashboardModuleInitializers || {};
        return window.homeownerDashboardModuleInitializers;
    }

    function normalizeModuleUrl(moduleUrl) {
        if (!moduleUrl) {
            return "/js/reservation-app.js";
        }

        try {
            return new URL(moduleUrl, window.location.origin).toString();
        } catch {
            return moduleUrl;
        }
    }

    function findReservationRoot(contentRoot) {
        if (!contentRoot) {
            return document.getElementById('reservation-react-root');
        }

        if (contentRoot.matches && contentRoot.matches('#reservation-react-root')) {
            return contentRoot;
        }

        if (contentRoot.querySelector) {
            return contentRoot.querySelector('#reservation-react-root');
        }

        return document.getElementById('reservation-react-root');
    }

    function resolveReservationModuleUrl(contentRoot) {
        const root = findReservationRoot(contentRoot);
        const appUrl = root?.dataset?.appUrl || window.__reservationAppUrl;
        return appUrl ? normalizeModuleUrl(appUrl) : null;
    }

    function getReservationModule(contentRoot) {
        const moduleUrl = resolveReservationModuleUrl(contentRoot);
        if (!moduleUrl) {
            return Promise.resolve(null);
        }

        if (!reservationModulePromise || reservationModuleUrl !== moduleUrl) {
            reservationModuleUrl = moduleUrl;
            reservationModulePromise = import(moduleUrl).catch((error) => {
                reservationModulePromise = null;
                reservationModuleUrl = null;
                throw error;
            });
        }

        return reservationModulePromise;
    }

    function prewarmReservationModule(contentRoot) {
        const moduleUrl = resolveReservationModuleUrl(contentRoot);
        if (!moduleUrl) {
            return;
        }

        getReservationModule(contentRoot).catch(() => {
        });
    }

    const initializeReservationModule = function (contentRoot) {
        const root = findReservationRoot(contentRoot);

        if (!root) {
            return;
        }

        if (root.dataset.reservationInitialized === 'true' || root.dataset.reservationInitializing === 'true') {
            return;
        }

        root.dataset.reservationInitializing = 'true';

        if (typeof window.__reservationAppCleanup === 'function') {
            window.__reservationAppCleanup();
            window.__reservationAppCleanup = null;
        }

        getReservationModule(root)
            .then((module) => {
                if (!module) {
                    root.dataset.reservationInitialized = 'false';
                    root.dataset.reservationInitializing = 'false';
                    return;
                }

                module.mountReservationApp(root);
                root.dataset.reservationInitialized = 'true';
                root.dataset.reservationInitializing = 'false';
            })
            .catch((error) => {
                console.error(error);
                root.dataset.reservationInitialized = 'false';
                root.dataset.reservationInitializing = 'false';
                root.innerHTML = '<div class="reservation-react-loading reservation-react-error">Unable to load the reservation experience.</div>';
            });
    };

    window.initializeReservationModule = initializeReservationModule;
    window.prewarmReservationModule = prewarmReservationModule;
    getDashboardModuleInitializers().reservation = initializeReservationModule;

})();
