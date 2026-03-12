(function () {
    function getDashboardModuleInitializers() {
        window.homeownerDashboardModuleInitializers = window.homeownerDashboardModuleInitializers || {};
        return window.homeownerDashboardModuleInitializers;
    }

    function normalize(value) {
        return (value || '').toString().trim().toLowerCase();
    }

    function applyFilters(root) {
        const query = normalize(root.querySelector('[data-contact-search]')?.value);
        const selectedCategory = root.dataset.selectedCategory || 'All';
        const cards = Array.from(root.querySelectorAll('[data-contact-card]'));
        let visibleCount = 0;

        cards.forEach((card) => {
            const matchesCategory = selectedCategory === 'All' || card.getAttribute('data-category') === selectedCategory;
            const matchesSearch = !query || normalize(card.getAttribute('data-search')).includes(query);
            const isVisible = matchesCategory && matchesSearch;

            card.classList.toggle('homeowner-contacts-hidden', !isVisible);
            if (isVisible) {
                visibleCount += 1;
            }
        });

        root.querySelectorAll('[data-contact-category]').forEach((button) => {
            button.classList.toggle('is-active', button.getAttribute('data-contact-category') === selectedCategory);
        });

        root.querySelector('[data-contact-empty]')?.classList.toggle('homeowner-contacts-hidden', visibleCount > 0);

        const countNode = root.querySelector('[data-contact-visible-count]');
        if (countNode) {
            countNode.textContent = visibleCount.toString();
        }
    }

    const initializeHomeownerContacts = function (contentRoot) {
        const root = contentRoot.querySelector('[data-homeowner-module="contacts"]')
            || (contentRoot.matches && contentRoot.matches('[data-homeowner-module="contacts"]') ? contentRoot : null);

        if (!root || root.dataset.initialized === 'true') {
            return;
        }

        root.dataset.initialized = 'true';

        root.addEventListener('click', function (event) {
            const categoryButton = event.target.closest('[data-contact-category]');
            if (!categoryButton) {
                return;
            }

            event.preventDefault();
            root.dataset.selectedCategory = categoryButton.getAttribute('data-contact-category') || 'All';
            applyFilters(root);
        });

        root.querySelector('[data-contact-search]')?.addEventListener('input', function () {
            applyFilters(root);
        });

        applyFilters(root);
    };

    window.initializeHomeownerContacts = initializeHomeownerContacts;
    getDashboardModuleInitializers().contacts = initializeHomeownerContacts;
})();
