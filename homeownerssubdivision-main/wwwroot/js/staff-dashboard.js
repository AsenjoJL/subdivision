(function () {
    const sectionCache = new Map();
    const pendingLoads = new Map();
    let profileUploadInput = null;
    const embeddedCleanupCallbacks = [];

    function getStaffModuleInitializers() {
        window.staffDashboardModuleInitializers = window.staffDashboardModuleInitializers || {};
        return window.staffDashboardModuleInitializers;
    }

    function runEmbeddedCleanup() {
        while (embeddedCleanupCallbacks.length) {
            const cleanup = embeddedCleanupCallbacks.pop();
            try {
                cleanup && cleanup();
            } catch (error) {
                console.error('Failed to clean up a staff dashboard module.', error);
            }
        }
    }

    function setTitle(titleElement, value) {
        if (titleElement) {
            titleElement.textContent = value || 'Staff Dashboard';
        }
    }

    function activateNav(activeLink, navLinks) {
        navLinks.forEach((link) => link.classList.remove('active'));
        activeLink?.classList.add('active');
    }

    function initializeDynamicContent(contentRoot) {
        const rootElement = contentRoot && contentRoot.jquery ? contentRoot[0] : contentRoot;
        if (!rootElement) {
            return;
        }

        Object.values(getStaffModuleInitializers()).forEach((initializer) => {
            if (typeof initializer === 'function') {
                initializer(rootElement);
            }
        });
    }

    function renderSection(html, title, context) {
        const { dynamicContent, titleElement } = context;
        if (!dynamicContent) {
            return;
        }

        runEmbeddedCleanup();
        dynamicContent.innerHTML = html;
        const loadedRoot = dynamicContent.firstElementChild;
        const nextTitle = loadedRoot?.dataset.pageTitle || title;
        setTitle(titleElement, nextTitle);
        initializeDynamicContent(loadedRoot || dynamicContent);
    }

    async function fetchSectionHtml(url) {
        if (!url) {
            throw new Error('Missing section URL.');
        }

        if (sectionCache.has(url)) {
            return sectionCache.get(url);
        }

        if (pendingLoads.has(url)) {
            return pendingLoads.get(url);
        }

        const request = fetch(url, {
            headers: {
                'X-Requested-With': 'XMLHttpRequest'
            }
        }).then(async (response) => {
            if (!response.ok) {
                throw new Error('Failed to load section.');
            }

            const html = await response.text();
            sectionCache.set(url, html);
            pendingLoads.delete(url);
            return html;
        }).catch((error) => {
            pendingLoads.delete(url);
            throw error;
        });

        pendingLoads.set(url, request);
        return request;
    }

    async function loadSection(url, title, context, options) {
        const settings = options || {};
        const { dynamicContent } = context;
        if (!url || !dynamicContent) {
            return;
        }

        if (!settings.skipLoadingState) {
            dynamicContent.classList.add('is-loading');
        }

        try {
            const html = settings.forceRefresh
                ? await fetch(url, {
                    headers: {
                        'X-Requested-With': 'XMLHttpRequest'
                    }
                }).then(async (response) => {
                    if (!response.ok) {
                        throw new Error('Failed to load section.');
                    }

                    const refreshed = await response.text();
                    sectionCache.set(url, refreshed);
                    return refreshed;
                })
                : await fetchSectionHtml(url);

            if (settings.onLoaded) {
                settings.onLoaded(html);
            }

            renderSection(html, title, context);
        } catch (error) {
            if (settings.onError) {
                settings.onError(error);
                return;
            }

            alert('Error loading content. Please try again.');
        } finally {
            if (!settings.skipLoadingState) {
                dynamicContent.classList.remove('is-loading');
            }
        }
    }

    document.addEventListener('DOMContentLoaded', function () {
        const shell = document.querySelector('[data-staff-dashboard]');
        if (!shell) {
            return;
        }

        const dashboardUrl = shell.getAttribute('data-dashboard-url');
        const managementUrl = shell.getAttribute('data-management-url');
        const profileUrl = shell.getAttribute('data-profile-url');
        const dynamicContent = document.getElementById('staff-dynamic-content');
        const titleElement = document.getElementById('staff-content-title');
        const navLinks = Array.from(shell.querySelectorAll('[data-staff-nav]'));
        const sidebar = document.getElementById('sidebar');
        const mobileToggle = document.querySelector('[data-staff-mobile-toggle]');
        const profileImageNodes = Array.from(shell.querySelectorAll('[data-staff-profile-image]'));

        const context = {
            dynamicContent,
            titleElement
        };

        window.__registerStaffEmbeddedCleanup = function (callback) {
            if (typeof callback === 'function') {
                embeddedCleanupCallbacks.push(callback);
            }
        };

        if (dashboardUrl && dynamicContent?.innerHTML) {
            sectionCache.set(dashboardUrl, dynamicContent.innerHTML);
        }

        window.staffDashboardShell = {
            loadSection: function (url, title, options) {
                return loadSection(url, title, context, options);
            },
            invalidate: function (url) {
                if (url) {
                    sectionCache.delete(url);
                }
            },
            updateCache: function (url, html) {
                if (url && html) {
                    sectionCache.set(url, html);
                }
            },
            getCache: function (url) {
                return url ? sectionCache.get(url) : null;
            },
            urls: {
                dashboard: dashboardUrl,
                management: managementUrl,
                profile: profileUrl
            },
            triggerProfileUpload: function () {
                if (!profileUploadInput) {
                    profileUploadInput = document.createElement('input');
                    profileUploadInput.type = 'file';
                    profileUploadInput.accept = '.jpg,.jpeg,.png,.webp,.gif';
                    profileUploadInput.style.display = 'none';
                    document.body.appendChild(profileUploadInput);

                    profileUploadInput.addEventListener('change', async function () {
                        const file = profileUploadInput.files && profileUploadInput.files[0];
                        profileUploadInput.value = '';

                        if (!file) {
                            return;
                        }

                        const extension = (file.name.split('.').pop() || '').toLowerCase();
                        if (!['jpg', 'jpeg', 'png', 'webp', 'gif'].includes(extension)) {
                            document.dispatchEvent(new CustomEvent('staff-profile-image:error', {
                                detail: { message: 'Please upload a JPG, PNG, WEBP, or GIF image.' }
                            }));
                            return;
                        }

                        if (file.size > 5 * 1024 * 1024) {
                            document.dispatchEvent(new CustomEvent('staff-profile-image:error', {
                                detail: { message: 'Profile images must be 5 MB or smaller.' }
                            }));
                            return;
                        }

                        try {
                            const formData = new FormData();
                            formData.append('profileImage', file);

                            const response = await fetch('/Admin/Staff/UpdateProfileImage', {
                                method: 'POST',
                                headers: {
                                    'X-Requested-With': 'XMLHttpRequest'
                                },
                                body: formData
                            });

                            const result = await response.json();
                            if (!response.ok) {
                                document.dispatchEvent(new CustomEvent('staff-profile-image:error', {
                                    detail: result
                                }));
                                return;
                            }

                            if (result.imagePath) {
                                profileImageNodes.forEach((node) => {
                                    node.src = result.imagePath;
                                });
                            }

                            if (result.dashboardUrl) {
                                sectionCache.delete(result.dashboardUrl);
                            }

                            if (result.profileUrl) {
                                sectionCache.delete(result.profileUrl);
                            }

                            document.dispatchEvent(new CustomEvent('staff-profile-image:uploaded', {
                                detail: result
                            }));
                        } catch (error) {
                            document.dispatchEvent(new CustomEvent('staff-profile-image:error', {
                                detail: { message: 'Unable to upload the profile picture right now.' }
                            }));
                        }
                    });
                }

                profileUploadInput.click();
            }
        };

        document.addEventListener('staff-profile-updated', function (event) {
            const detail = event.detail || {};
            if (detail.staffName) {
                document.querySelectorAll('[data-staff-display-name]').forEach((node) => {
                    node.textContent = detail.staffName;
                });
            }
        });

        navLinks.forEach((item, index) => {
            item.style.opacity = '0';
            item.style.transform = 'translateX(-20px)';
            setTimeout(() => {
                item.style.transition = 'all 0.3s ease';
                item.style.opacity = '1';
                item.style.transform = 'translateX(0)';
            }, index * 50);
        });

        mobileToggle?.addEventListener('click', function () {
            sidebar?.classList.toggle('active');
        });

        navLinks.forEach((link) => {
            link.addEventListener('click', function (event) {
                event.preventDefault();
                activateNav(link, navLinks);

                const target = link.getAttribute('data-target');
                const title = link.getAttribute('data-target-title');
                const url = target === 'management'
                    ? managementUrl
                    : target === 'profile'
                        ? profileUrl
                        : dashboardUrl;

                loadSection(url, title, context);

                if (window.innerWidth <= 768) {
                    sidebar?.classList.remove('active');
                }
            });
        });

        dynamicContent?.addEventListener('click', function (event) {
            const quickAction = event.target.closest('[data-staff-action="management"]');
            if (!quickAction) {
                return;
            }

            event.preventDefault();

            const managementLink = navLinks.find((link) => link.getAttribute('data-target') === 'management');
            activateNav(managementLink, navLinks);
            loadSection(managementUrl, 'Task Management', context);
        });

        initializeDynamicContent(dynamicContent);
    });
})();
