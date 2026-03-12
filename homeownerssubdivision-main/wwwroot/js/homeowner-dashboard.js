(function () {
    const sectionCache = new Map();
    const pendingLoads = new Map();
    const CACHE_TTL_MS = 60000;
    const PREFETCH_STAGGER_MS = 160;
    let activeLoadToken = 0;
    let isProfileUploadInProgress = false;
    window.__homeownerEmbeddedCleanups = window.__homeownerEmbeddedCleanups || [];
    window.__registerHomeownerEmbeddedCleanup = function registerHomeownerEmbeddedCleanup(cleanup) {
        if (typeof cleanup === 'function') {
            window.__homeownerEmbeddedCleanups.push(cleanup);
        }
    };

    function getDashboardModuleInitializers() {
        window.homeownerDashboardModuleInitializers = window.homeownerDashboardModuleInitializers || {};
        return window.homeownerDashboardModuleInitializers;
    }

    function cleanupDynamicContent() {
        if (!Array.isArray(window.__homeownerEmbeddedCleanups)) {
            return;
        }

        while (window.__homeownerEmbeddedCleanups.length > 0) {
            const cleanup = window.__homeownerEmbeddedCleanups.pop();
            try {
                cleanup?.();
            } catch (error) {
                console.error(error);
            }
        }
    }

    function initializeDynamicContent(contentRoot) {
        const rootElement = contentRoot && contentRoot.jquery ? contentRoot[0] : contentRoot;
        if (!rootElement) {
            return;
        }

        Object.values(getDashboardModuleInitializers()).forEach((initializer) => {
            if (typeof initializer === 'function') {
                initializer(rootElement);
            }
        });
    }

    function executeEmbeddedScripts(container) {
        if (!container || !container.querySelectorAll) {
            return;
        }

        const scripts = container.querySelectorAll('script');
        scripts.forEach((script) => {
            const replacement = document.createElement('script');
            Array.from(script.attributes).forEach((attribute) => {
                replacement.setAttribute(attribute.name, attribute.value);
            });
            replacement.textContent = script.textContent;
            script.parentNode?.replaceChild(replacement, script);
        });
    }

    function setShellState(pageTitleElement, forumButton, backButton, isHomeView, pageTitle) {
        if (pageTitleElement) {
            pageTitleElement.textContent = pageTitle || 'Content';
        }

        if (forumButton) {
            forumButton.style.display = isHomeView ? '' : 'none';
        }

        if (backButton) {
            backButton.style.display = isHomeView ? 'none' : '';
        }
    }

    function activateNavLink(activeLink, allLinks) {
        allLinks.forEach((link) => link.classList.remove('active'));
        activeLink?.classList.add('active');
    }

    function updateProfileImages(imagePath) {
        if (!imagePath) {
            return;
        }

        document.querySelectorAll('[data-homeowner-profile-image]').forEach((imageNode) => {
            imageNode.src = imagePath;
        });
    }

    function applyProfileImageFallbacks(root) {
        const imageNodes = root?.querySelectorAll
            ? root.querySelectorAll('[data-homeowner-profile-image]')
            : document.querySelectorAll('[data-homeowner-profile-image]');

        imageNodes.forEach((imageNode) => {
            if (imageNode.dataset.fallbackBound === 'true') {
                return;
            }

            imageNode.dataset.fallbackBound = 'true';
            imageNode.addEventListener('error', function () {
                const fallbackSrc = imageNode.getAttribute('data-fallback-src');
                if (!fallbackSrc || imageNode.src.endsWith(fallbackSrc)) {
                    return;
                }

                imageNode.src = fallbackSrc;
            });
        });
    }

    function dispatchProfileUploadEvent(name, detail) {
        document.dispatchEvent(new CustomEvent(name, { detail: detail || {} }));
    }

    function shouldDeferPrefetch() {
        return isProfileUploadInProgress;
    }

    function setProfileUploadButtonState(buttons, options) {
        const settings = options || {};
        const progress = typeof settings.progress === 'number'
            ? Math.max(0, Math.min(100, Math.round(settings.progress)))
            : null;

        buttons.forEach((button) => {
            if (!button) {
                return;
            }

            const labelNode = button.querySelector('span');
            const iconNode = button.querySelector('i');
            const originalLabel = button.dataset.originalLabel || 'Change Photo';
            const originalIconClass = button.dataset.originalIconClass || 'fas fa-camera';

            if (settings.isUploading) {
                button.disabled = true;
                button.classList.add('is-uploading');
                const nextLabel = progress !== null && progress > 0 && progress < 100
                    ? `Uploading ${progress}%...`
                    : 'Uploading...';

                if (iconNode) {
                    iconNode.className = 'fas fa-spinner';
                }

                if (labelNode) {
                    labelNode.textContent = nextLabel;
                } else {
                    button.textContent = nextLabel;
                }

                return;
            }

            button.disabled = false;
            button.classList.remove('is-uploading');
            if (iconNode) {
                iconNode.className = originalIconClass;
            }
            if (labelNode) {
                labelNode.textContent = originalLabel;
            } else {
                button.textContent = originalLabel;
            }
        });
    }

    function uploadProfileImage(url, formData) {
        return new Promise((resolve, reject) => {
            const xhr = new XMLHttpRequest();
            xhr.open('POST', url, true);
            xhr.setRequestHeader('X-Requested-With', 'XMLHttpRequest');
            xhr.responseType = 'json';

            xhr.upload.addEventListener('progress', function (event) {
                if (!event.lengthComputable) {
                    dispatchProfileUploadEvent('homeowner-profile-image:progress', { progress: null });
                    return;
                }

                dispatchProfileUploadEvent('homeowner-profile-image:progress', {
                    progress: (event.loaded / event.total) * 100
                });
            });

            xhr.addEventListener('load', function () {
                const rawResponse = xhr.response
                    || (xhr.responseText ? JSON.parse(xhr.responseText) : {});

                if (xhr.status >= 200 && xhr.status < 300) {
                    resolve(rawResponse);
                    return;
                }

                reject(new Error(rawResponse?.message || 'Unable to upload your profile picture right now.'));
            });

            xhr.addEventListener('error', function () {
                reject(new Error('Unable to upload your profile picture right now.'));
            });

            xhr.addEventListener('abort', function () {
                reject(new Error('Profile picture upload was cancelled.'));
            });

            xhr.send(formData);
        });
    }

    function findNavLinkByUrl(navLinks, url) {
        return navLinks.find((link) => link.getAttribute('data-load-url') === url) || null;
    }

    function setCachedSection(url, html) {
        if (!url || !html) {
            return;
        }

        sectionCache.set(url, {
            html: html,
            cachedAt: Date.now()
        });
    }

    function getCachedSectionEntry(url) {
        return url ? sectionCache.get(url) || null : null;
    }

    function getCachedSectionHtml(url) {
        return getCachedSectionEntry(url)?.html || null;
    }

    function isCacheFresh(url) {
        const entry = getCachedSectionEntry(url);
        if (!entry) {
            return false;
        }

        return (Date.now() - entry.cachedAt) < CACHE_TTL_MS;
    }

    function renderContent(html, context) {
        const { dynamicContent, pageTitleElement, forumButton, backButton } = context;
        if (!dynamicContent) {
            return;
        }

        cleanupDynamicContent();
        dynamicContent.innerHTML = html;
        executeEmbeddedScripts(dynamicContent);
        const loadedRoot = dynamicContent.firstElementChild;
        const pageTitle = loadedRoot?.dataset.pageTitle
            || loadedRoot?.querySelector?.('h2')?.textContent
            || 'Content';
        const isHomeView = loadedRoot?.dataset.homeView === 'true';

        setShellState(pageTitleElement, forumButton, backButton, isHomeView, pageTitle);
        initializeDynamicContent(dynamicContent);
        applyProfileImageFallbacks(dynamicContent);
    }

    async function fetchSectionHtml(url, options) {
        const settings = options || {};
        if (!url) {
            throw new Error('Missing section URL.');
        }

        if (!settings.forceRefresh) {
            const cachedHtml = getCachedSectionHtml(url);
            if (cachedHtml) {
                return cachedHtml;
            }
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
            setCachedSection(url, html);
            pendingLoads.delete(url);
            return html;
        }).catch((error) => {
            pendingLoads.delete(url);
            throw error;
        });

        pendingLoads.set(url, request);
        return request;
    }

    async function refreshSectionCache(url) {
        return fetchSectionHtml(url, { forceRefresh: true });
    }

    function refreshSectionCacheInBackground(url, onLoaded) {
        refreshSectionCache(url).then((html) => {
            if (typeof onLoaded === 'function') {
                onLoaded(html);
            }
        }).catch(function () {
        });
    }

    async function loadContent(url, context, options) {
        const { dynamicContent } = context;
        const settings = options || {};
        if (!url || !dynamicContent) {
            return;
        }

        const loadToken = ++activeLoadToken;

        if (!settings.forceRefresh) {
            const cachedHtml = getCachedSectionHtml(url);
            if (cachedHtml) {
                renderContent(cachedHtml, context);

                if (settings.onLoaded) {
                    settings.onLoaded(cachedHtml);
                }

                if (!isCacheFresh(url) || settings.revalidate === true) {
                    refreshSectionCacheInBackground(url, settings.onLoaded);
                }

                return;
            }
        }

        if (!settings.skipLoadingState) {
            dynamicContent.classList.add('is-loading');
        }

        try {
            const html = await fetchSectionHtml(url, { forceRefresh: settings.forceRefresh });

            if (loadToken !== activeLoadToken) {
                if (settings.onLoaded) {
                    settings.onLoaded(html);
                }
                return;
            }

            if (settings.onLoaded) {
                settings.onLoaded(html);
            }

            renderContent(html, context);
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
        const shell = document.querySelector('[data-homeowner-dashboard]');
        if (!shell) {
            return;
        }

        const homeUrl = shell.getAttribute('data-home-url');
        const forumUrl = shell.getAttribute('data-forum-url');
        const sidebar = document.getElementById('sidebar');
        const dynamicContent = document.getElementById('dynamic-content');
        const pageTitleElement = document.getElementById('content-title');
        const forumButton = document.getElementById('forum-btn');
        const backButton = document.getElementById('back-to-dashboard-btn');
        const navLinks = Array.from(shell.querySelectorAll('[data-ajax-nav]'));
        const mobileMenuToggle = document.querySelector('[data-mobile-menu-toggle]');
        const uploadInput = document.getElementById('upload-input');
        const profileUploadUrl = shell.getAttribute('data-profile-upload-url');
        let homeSnapshot = dynamicContent?.innerHTML || '';

        const context = {
            dynamicContent,
            pageTitleElement,
            forumButton,
            backButton
        };

        window.loadContent = function (url) {
            return loadContent(url, context);
        };

        window.homeownerDashboardShell = {
            loadContent: window.loadContent,
            invalidate: function (url) {
                if (url) {
                    sectionCache.delete(url);
                    if (url === homeUrl) {
                        homeSnapshot = '';
                    }
                }
            },
            updateCache: function (url, html) {
                if (url && html) {
                    setCachedSection(url, html);
                    if (url === homeUrl) {
                        homeSnapshot = html;
                    }
                }
            },
            getCache: function (url) {
                return getCachedSectionHtml(url);
            }
        };

        if (homeUrl && homeSnapshot) {
            setCachedSection(homeUrl, homeSnapshot);
        }

        applyProfileImageFallbacks(document);

        navLinks.forEach((item, index) => {
            item.style.opacity = '0';
            item.style.transform = 'translateX(-20px)';
            setTimeout(() => {
                item.style.transition = 'all 0.3s ease';
                item.style.opacity = '1';
                item.style.transform = 'translateX(0)';
            }, index * 50);
        });

        uploadInput?.addEventListener('change', function (event) {
            const file = event.target.files?.[0];
            if (!file || !profileUploadUrl) {
                return;
            }

            const allowedTypes = new Set(['image/jpeg', 'image/png', 'image/webp', 'image/gif']);
            if (!allowedTypes.has(file.type)) {
                dispatchProfileUploadEvent('homeowner-profile-image:error', {
                    message: 'Please choose a JPG, PNG, WEBP, or GIF image.'
                });
                uploadInput.value = '';
                return;
            }

            if (file.size > 5 * 1024 * 1024) {
                dispatchProfileUploadEvent('homeowner-profile-image:error', {
                    message: 'Profile images must be 5 MB or smaller.'
                });
                uploadInput.value = '';
                return;
            }

            const triggerButtons = Array.from(document.querySelectorAll('[data-profile-upload-trigger]'));
            triggerButtons.forEach((button) => {
                button.dataset.originalLabel = button.querySelector('span')?.textContent || button.textContent.trim();
                button.dataset.originalIconClass = button.querySelector('i')?.className || 'fas fa-camera';
            });
            isProfileUploadInProgress = true;
            setProfileUploadButtonState(triggerButtons, { isUploading: true });
            dispatchProfileUploadEvent('homeowner-profile-image:started', {
                fileName: file.name
            });

            const formData = new FormData();
            formData.append('profileImage', file);

            uploadProfileImage(profileUploadUrl, formData).then((result) => {
                if (!result.imagePath) {
                    throw new Error(result.message || 'Unable to upload your profile picture right now.');
                }

                updateProfileImages(result.imagePath);
                if (window.homeownerDashboardShell?.invalidate) {
                    if (result.homeUrl) {
                        window.homeownerDashboardShell.invalidate(result.homeUrl);
                    }
                    if (result.settingsUrl) {
                        window.homeownerDashboardShell.invalidate(result.settingsUrl);
                    }
                }
                dispatchProfileUploadEvent('homeowner-profile-image:uploaded', {
                    imagePath: result.imagePath,
                    message: result.message || 'Profile picture updated successfully.'
                });
            }).catch((error) => {
                dispatchProfileUploadEvent('homeowner-profile-image:error', {
                    message: error.message || 'Unable to upload your profile picture right now.'
                });
            }).finally(() => {
                isProfileUploadInProgress = false;
                setProfileUploadButtonState(triggerButtons, { isUploading: false });
                uploadInput.value = '';
            });
        });

        document.addEventListener('homeowner-profile-image:progress', function (event) {
            const triggerButtons = Array.from(document.querySelectorAll('[data-profile-upload-trigger]'));
            setProfileUploadButtonState(triggerButtons, {
                isUploading: true,
                progress: event.detail?.progress
            });
        });

        document.addEventListener('click', function (event) {
            const uploadTrigger = event.target.closest('[data-profile-upload-trigger]');
            if (!uploadTrigger) {
                return;
            }

            event.preventDefault();
            uploadInput?.click();
        });

        mobileMenuToggle?.addEventListener('click', function () {
            sidebar?.classList.toggle('active');
        });

        navLinks.forEach((link) => {
            link.addEventListener('click', function (event) {
                event.preventDefault();
                const url = link.getAttribute('data-load-url');
                activateNavLink(link, navLinks);
                if (url === homeUrl && homeSnapshot) {
                    renderContent(homeSnapshot, context);
                } else {
                    loadContent(url, context, {
                        onLoaded: function (html) {
                            if (url === homeUrl) {
                                homeSnapshot = html;
                            }
                        }
                    });
                }

                if (window.innerWidth <= 768) {
                    sidebar?.classList.remove('active');
                }
            });

            const prefetchLinkTarget = function () {
                const url = link.getAttribute('data-load-url');
                if (url && !shouldDeferPrefetch()) {
                    fetchSectionHtml(url).catch(function () {
                    });
                }
            };

            link.addEventListener('pointerenter', prefetchLinkTarget, { passive: true });
            link.addEventListener('focus', prefetchLinkTarget, { passive: true });
            link.addEventListener('pointerdown', prefetchLinkTarget, { passive: true });
            link.addEventListener('touchstart', prefetchLinkTarget, { passive: true });
        });

        dynamicContent?.addEventListener('click', function (event) {
            const actionLink = event.target.closest('[data-dashboard-link]');
            if (!actionLink) {
                return;
            }

            event.preventDefault();

            const url = actionLink.getAttribute('data-load-url');
            const matchedNavUrl = actionLink.getAttribute('data-nav-match') || url;
            const matchingNavLink = findNavLinkByUrl(navLinks, matchedNavUrl);

            if (matchingNavLink) {
                activateNavLink(matchingNavLink, navLinks);
            }

            if (url === homeUrl && homeSnapshot) {
                renderContent(homeSnapshot, context);
                return;
            }

            loadContent(url, context, {
                onLoaded: function (html) {
                    if (url === homeUrl) {
                        homeSnapshot = html;
                    }
                }
            });
        });

        dynamicContent?.addEventListener('pointerenter', function (event) {
            const actionLink = event.target.closest('[data-dashboard-link]');
            const url = actionLink?.getAttribute('data-load-url');
            if (url && !shouldDeferPrefetch()) {
                fetchSectionHtml(url).catch(function () {
                });
            }
        }, { passive: true });

        dynamicContent?.addEventListener('pointerdown', function (event) {
            const actionLink = event.target.closest('[data-dashboard-link]');
            const url = actionLink?.getAttribute('data-load-url');
            if (url) {
                fetchSectionHtml(url).catch(function () {
                });
            }
        }, { passive: true });

        forumButton?.addEventListener('click', function () {
            if (!forumUrl) {
                return;
            }

            loadContent(forumUrl, context);
        });

        backButton?.addEventListener('click', function () {
            const homeLink = navLinks[0];
            activateNavLink(homeLink, navLinks);
            if (homeSnapshot) {
                renderContent(homeSnapshot, context);
                return;
            }

            loadContent(homeUrl, context, {
                onLoaded: function (html) {
                    homeSnapshot = html;
                }
            });
        });

        initializeDynamicContent(dynamicContent);

        const highPriorityPrefetchTargets = navLinks
            .filter((link) => link.getAttribute('data-prefetch-nav') === 'true')
            .map((link) => link.getAttribute('data-load-url'))
            .filter(Boolean);
        const allSidebarTargets = navLinks
            .map((link) => link.getAttribute('data-load-url'))
            .filter(Boolean);
        const lowPriorityPrefetchTargets = allSidebarTargets
            .filter((url, index, array) => array.indexOf(url) === index)
            .filter((url) => !highPriorityPrefetchTargets.includes(url));

        const prefetchSections = function (targets, initialDelay) {
            targets.forEach((url, index) => {
                window.setTimeout(function () {
                    if (shouldDeferPrefetch()) {
                        return;
                    }
                    fetchSectionHtml(url).catch(function () {
                    });
                }, (initialDelay || 0) + (index * PREFETCH_STAGGER_MS));
            });
        };

        window.setTimeout(function () {
            prefetchSections(highPriorityPrefetchTargets, 0);
            prefetchSections(lowPriorityPrefetchTargets, 400);
        }, 120);
    });
})();
