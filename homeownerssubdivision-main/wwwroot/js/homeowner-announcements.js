(function () {
    function getDashboardModuleInitializers() {
        window.homeownerDashboardModuleInitializers = window.homeownerDashboardModuleInitializers || {};
        return window.homeownerDashboardModuleInitializers;
    }

    function applyFilters(root) {
        const query = (root.querySelector('[data-home-announcement-search]')?.value || '').trim().toLowerCase();
        const filter = root.dataset.selectedAnnouncementFilter || 'All';
        let visible = 0;

        root.querySelectorAll('[data-home-announcement-card]').forEach((card) => {
            const searchValue = (card.getAttribute('data-search') || '').toLowerCase();
            const isUrgent = card.getAttribute('data-home-announcement-urgent') === 'true';
            const matchesSearch = !query || searchValue.includes(query);
            const matchesFilter = filter === 'All' || (filter === 'Urgent' ? isUrgent : !isUrgent);
            const shouldShow = matchesSearch && matchesFilter;
            card.classList.toggle('homeowner-announcements-hidden', !shouldShow);
            if (shouldShow) {
                visible += 1;
            }
        });

        root.querySelectorAll('[data-home-announcement-filter]').forEach((button) => {
            button.classList.toggle('is-active', button.getAttribute('data-home-announcement-filter') === filter);
        });

        root.querySelector('[data-home-announcement-empty]')?.classList.toggle('homeowner-announcements-hidden', visible > 0);
        const totalNode = root.querySelector('[data-home-announcement-total]');
        if (totalNode) {
            totalNode.textContent = visible.toString();
        }
    }

    const initializeHomeownerAnnouncements = function (contentRoot) {
        const root = contentRoot.querySelector('[data-homeowner-module="announcements"]')
            || (contentRoot.matches && contentRoot.matches('[data-homeowner-module="announcements"]') ? contentRoot : null);

        if (!root || root.dataset.initialized === 'true') {
            return;
        }

        root.dataset.initialized = 'true';
        root.dataset.selectedAnnouncementFilter = 'All';

        root.addEventListener('click', function (event) {
            const button = event.target.closest('[data-home-announcement-filter]');
            if (!button) {
                return;
            }

            event.preventDefault();
            root.dataset.selectedAnnouncementFilter = button.getAttribute('data-home-announcement-filter') || 'All';
            applyFilters(root);
        });

        root.querySelector('[data-home-announcement-search]')?.addEventListener('input', function () {
            applyFilters(root);
        });

        applyFilters(root);
    };

    window.initializeHomeownerAnnouncements = initializeHomeownerAnnouncements;
    getDashboardModuleInitializers().announcements = initializeHomeownerAnnouncements;
})();
