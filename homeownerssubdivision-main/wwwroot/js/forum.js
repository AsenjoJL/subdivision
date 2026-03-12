(function () {
    function getDashboardModuleInitializers() {
        window.homeownerDashboardModuleInitializers = window.homeownerDashboardModuleInitializers || {};
        return window.homeownerDashboardModuleInitializers;
    }

    function findForumRoot(contentRoot) {
        if (!contentRoot) {
            return null;
        }

        if (contentRoot.matches && contentRoot.matches('[data-forum-root]')) {
            return contentRoot;
        }

        return contentRoot.querySelector ? contentRoot.querySelector('[data-forum-root]') : null;
    }

    function setAlert(root, message, type) {
        const alertBox = root.querySelector('[data-forum-alert]');
        if (!alertBox) {
            return;
        }

        window.clearTimeout(root.__forumAlertTimer);

        if (!message) {
            alertBox.textContent = '';
            alertBox.className = 'forum-alert';
            return;
        }

        alertBox.textContent = message;
        alertBox.className = `forum-alert is-visible ${type === 'success' ? 'is-success' : 'is-error'}`;
        root.__forumAlertTimer = window.setTimeout(function () {
            alertBox.textContent = '';
            alertBox.className = 'forum-alert';
        }, 3200);
    }

    function getLatestActivityTicks(root) {
        const value = Number.parseInt(root.getAttribute('data-latest-activity-ticks') || '0', 10);
        return Number.isFinite(value) ? value : 0;
    }

    async function parseError(response) {
        const contentType = response.headers.get('content-type') || '';
        if (contentType.includes('application/json')) {
            const payload = await response.json();
            return payload.message || payload.error || 'The request could not be completed.';
        }

        const text = (await response.text()).trim();
        return text || 'The request could not be completed.';
    }

    async function refreshRoot(root, successMessage) {
        const refreshUrl = root.getAttribute('data-refresh-url');
        if (!refreshUrl) {
            return;
        }

        const response = await fetch(refreshUrl, {
            headers: {
                'X-Requested-With': 'XMLHttpRequest'
            },
            credentials: 'same-origin'
        });

        if (!response.ok) {
            throw new Error('Unable to refresh the forum.');
        }

        const html = await response.text();
        const wrapper = document.createElement('div');
        wrapper.innerHTML = html.trim();
        const newRoot = wrapper.querySelector('[data-forum-root]');

        if (!newRoot) {
            throw new Error('The refreshed forum markup was invalid.');
        }

        root.replaceWith(newRoot);
        initializeForumModule(newRoot);
        if (successMessage) {
            setAlert(newRoot, successMessage, 'success');
        }
    }

    async function updateLatestActivity(root) {
        const feedStateUrl = root.getAttribute('data-feed-state-url');
        if (!feedStateUrl) {
            return;
        }

        const response = await fetch(feedStateUrl, {
            headers: {
                'X-Requested-With': 'XMLHttpRequest',
                'Accept': 'application/json'
            },
            credentials: 'same-origin'
        });

        if (!response.ok) {
            throw new Error('Unable to check the latest forum activity.');
        }

        const payload = await response.json();
        root.setAttribute('data-latest-activity-ticks', String(payload.latestActivityTicks || 0));
    }

    async function refreshFeed(root, successMessage) {
        const feedHost = root.querySelector('[data-forum-feed-host]');
        const feedUrl = root.getAttribute('data-feed-url');
        if (!feedHost || !feedUrl) {
            return;
        }

        feedHost.classList.add('is-loading');

        try {
            const response = await fetch(feedUrl, {
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                },
                credentials: 'same-origin'
            });

            if (!response.ok) {
                throw new Error('Unable to refresh the forum feed.');
            }

            feedHost.innerHTML = await response.text();
            await updateLatestActivity(root);
            if (successMessage) {
                setAlert(root, successMessage, 'success');
            }
        } finally {
            feedHost.classList.remove('is-loading');
        }
    }

    async function refreshPostCard(root, postId, successMessage) {
        const cardUrlTemplate = root.getAttribute('data-post-card-url-template');
        if (!cardUrlTemplate || !postId) {
            await refreshFeed(root, successMessage);
            return;
        }

        const postCard = root.querySelector(`.forum-post-card[data-post-id="${postId}"]`);
        if (!postCard) {
            await refreshFeed(root, successMessage);
            return;
        }

        const cardUrl = cardUrlTemplate.replace('__ID__', encodeURIComponent(postId));
        const response = await fetch(cardUrl, {
            headers: {
                'X-Requested-With': 'XMLHttpRequest'
            },
            credentials: 'same-origin'
        });

        if (!response.ok) {
            throw new Error('Unable to refresh this post right now.');
        }

        const html = await response.text();
        const wrapper = document.createElement('div');
        wrapper.innerHTML = html.trim();
        const newCard = wrapper.querySelector('.forum-post-card');

        if (!newCard) {
            throw new Error('The refreshed post markup was invalid.');
        }

        postCard.replaceWith(newCard);
        await updateLatestActivity(root);
        if (successMessage) {
            setAlert(root, successMessage, 'success');
        }
    }

    async function replacePostCardFromHtml(root, postId, html, successMessage) {
        const postCard = root.querySelector(`.forum-post-card[data-post-id="${postId}"]`);
        if (!postCard) {
            await refreshFeed(root, successMessage);
            return;
        }

        const wrapper = document.createElement('div');
        wrapper.innerHTML = (html || '').trim();
        const newCard = wrapper.querySelector('.forum-post-card');

        if (!newCard) {
            await refreshPostCard(root, postId, successMessage);
            return;
        }

        postCard.replaceWith(newCard);
        await updateLatestActivity(root);
        if (successMessage) {
            setAlert(root, successMessage, 'success');
        }
    }

    async function submitMultipartForm(root, form, successMessage) {
        setAlert(root, '');
        root.__forumBusy = true;

        const submitButton = form.querySelector("button[type='submit']");
        const defaultLabel = submitButton?.getAttribute('data-forum-submit-label') || submitButton?.textContent?.trim() || 'Save';

        if (submitButton) {
            submitButton.disabled = true;
            submitButton.textContent = 'Saving...';
        }

        try {
            const response = await fetch(form.action, {
                method: 'POST',
                body: new FormData(form),
                credentials: 'same-origin'
            });

            if (!response.ok) {
                throw new Error(await parseError(response));
            }

            await refreshRoot(root, successMessage);
        } catch (error) {
            setAlert(root, error.message || 'The request could not be completed.', 'error');
            if (submitButton) {
                submitButton.disabled = false;
                submitButton.textContent = defaultLabel;
            }
        } finally {
            root.__forumBusy = false;
        }
    }

    function getSuccessMessage(element, fallbackMessage) {
        return element?.getAttribute('data-forum-success-message') || fallbackMessage;
    }

    function registerCleanup(root, cleanup) {
        if (root.__forumCleanupRegistered) {
            return;
        }

        root.__forumCleanupRegistered = true;
        if (typeof window.__registerHomeownerEmbeddedCleanup === 'function') {
            window.__registerHomeownerEmbeddedCleanup(cleanup);
        }
        if (typeof window.__registerAdminEmbeddedCleanup === 'function') {
            window.__registerAdminEmbeddedCleanup(cleanup);
        }
    }

    function startRealtimeSync(root) {
        const feedStateUrl = root.getAttribute('data-feed-state-url');
        if (!feedStateUrl || root.__forumRealtimeStarted) {
            return;
        }

        root.__forumRealtimeStarted = true;

        const pollForUpdates = async function () {
            if (document.hidden || root.__forumBusy) {
                return;
            }

            try {
                const response = await fetch(feedStateUrl, {
                    headers: {
                        'X-Requested-With': 'XMLHttpRequest',
                        'Accept': 'application/json'
                    },
                    credentials: 'same-origin'
                });

                if (!response.ok) {
                    return;
                }

                const payload = await response.json();
                const serverTicks = Number(payload.latestActivityTicks || 0);
                const currentTicks = getLatestActivityTicks(root);

                if (serverTicks > currentTicks) {
                    root.__forumBusy = true;
                    root.setAttribute('data-latest-activity-ticks', String(serverTicks));
                    await refreshFeed(root);
                }
            } catch (error) {
            } finally {
                root.__forumBusy = false;
            }
        };

        root.__forumPollTimer = window.setInterval(pollForUpdates, 10000);
        root.__forumVisibilityHandler = function () {
            if (!document.hidden) {
                pollForUpdates();
            }
        };

        document.addEventListener('visibilitychange', root.__forumVisibilityHandler);
    }

    function initializeForumModule(contentRoot) {
        const root = findForumRoot(contentRoot);
        if (!root || root.dataset.initialized === 'true') {
            return;
        }

        root.dataset.initialized = 'true';

        const handleClick = async function (event) {
            const toggleButton = event.target.closest('.forum-toggle-comments');
            if (toggleButton) {
                const postCard = toggleButton.closest('.forum-post-card');
                const commentThread = postCard?.querySelector('.forum-comment-thread');
                commentThread?.classList.toggle('is-open');
                return;
            }

            const reactionButton = event.target.closest('.forum-react-btn');
            if (!reactionButton) {
                return;
            }

            const postCard = reactionButton.closest('.forum-post-card');
            const postId = postCard?.getAttribute('data-post-id');
            const reactionType = reactionButton.getAttribute('data-reaction-type');
            const reactionUrl = root.getAttribute('data-reaction-url');

            if (!postId || !reactionType || !reactionUrl) {
                return;
            }

            const formData = new FormData();
            formData.append('postId', postId);
            formData.append('reactionType', reactionType);

            try {
                const response = await fetch(reactionUrl, {
                    method: 'POST',
                    body: formData,
                    headers: {
                        'X-Requested-With': 'XMLHttpRequest'
                    },
                    credentials: 'same-origin'
                });

                if (!response.ok) {
                    throw new Error(await parseError(response));
                }

                const contentType = response.headers.get('content-type') || '';
                const successMessage = getSuccessMessage(reactionButton, 'Reaction saved successfully.');
                if (contentType.includes('text/html')) {
                    await replacePostCardFromHtml(root, postId, await response.text(), successMessage);
                } else {
                    await refreshPostCard(root, postId, successMessage);
                }
            } catch (error) {
                setAlert(root, error.message || 'Unable to save the reaction.', 'error');
            }
        };

        const handleSubmit = async function (event) {
            const form = event.target;
            if (!(form instanceof HTMLFormElement)) {
                return;
            }

            if (!form.matches('.forum-form, .forum-comment-form, .forum-settings-form')) {
                return;
            }

            event.preventDefault();

            const successMessage = getSuccessMessage(form, 'Forum updated successfully.');
            if (form.matches('.forum-settings-form')) {
                await submitMultipartForm(root, form, successMessage);
                return;
            }

            setAlert(root, '');
            root.__forumBusy = true;

            const submitButton = form.querySelector("button[type='submit']");
            const defaultLabel = submitButton?.getAttribute('data-forum-submit-label') || submitButton?.textContent?.trim() || 'Save';

            if (submitButton) {
                submitButton.disabled = true;
                submitButton.textContent = 'Saving...';
            }

            try {
                const postId = form.querySelector('input[name="postId"]')?.value || '';
                const response = await fetch(form.action, {
                    method: 'POST',
                    body: new FormData(form),
                    headers: {
                        'X-Requested-With': 'XMLHttpRequest'
                    },
                    credentials: 'same-origin'
                });

                if (!response.ok) {
                    throw new Error(await parseError(response));
                }

                const contentType = response.headers.get('content-type') || '';
                if (form.matches('.forum-comment-form') && postId) {
                    if (contentType.includes('text/html')) {
                        await replacePostCardFromHtml(root, postId, await response.text(), successMessage);
                    } else {
                        await refreshPostCard(root, postId, successMessage);
                    }
                } else {
                    await refreshFeed(root, successMessage);
                    form.reset();
                }
            } catch (error) {
                setAlert(root, error.message || 'The request could not be completed.', 'error');
            } finally {
                root.__forumBusy = false;
                if (submitButton) {
                    submitButton.disabled = false;
                    submitButton.textContent = defaultLabel;
                }
            }
        };

        root.addEventListener('click', handleClick);
        root.addEventListener('submit', handleSubmit);
        startRealtimeSync(root);
        refreshFeed(root).catch(function (error) {
            setAlert(root, error.message || 'Unable to load the forum feed right now.', 'error');
        });

        registerCleanup(root, function () {
            window.clearTimeout(root.__forumAlertTimer);
            window.clearInterval(root.__forumPollTimer);
            if (root.__forumVisibilityHandler) {
                document.removeEventListener('visibilitychange', root.__forumVisibilityHandler);
            }
            root.removeEventListener('click', handleClick);
            root.removeEventListener('submit', handleSubmit);
        });
    }

    window.initializeForumModule = initializeForumModule;
    getDashboardModuleInitializers().forum = initializeForumModule;

    document.addEventListener('DOMContentLoaded', function () {
        initializeForumModule(document);
    });
})();
