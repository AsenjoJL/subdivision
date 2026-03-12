(function () {
    function initializeBillingModule(rootElement) {
        const root = rootElement?.matches?.('.payment-module')
            ? rootElement
            : rootElement?.querySelector?.('.payment-module');

        if (!root || root.dataset.billingBound === 'true') {
            return;
        }

        root.dataset.billingBound = 'true';

        const token = root.querySelector('.payment-token-form input[name="__RequestVerificationToken"]')?.value || '';
        const modal = document.getElementById(root.dataset.modalId || '');
        const form = document.getElementById(root.dataset.formId || '');
        const submitButton = document.getElementById(root.dataset.submitButtonId || '');
        const sectionAlert = root.querySelector('[data-payment-section-alert]');
        const modalAlert = document.getElementById(root.dataset.modalAlertId || '');
        const contextLabel = document.getElementById(root.dataset.modalContextId || '');

        if (!modal || !form || !submitButton || !sectionAlert || !modalAlert || !contextLabel) {
            return;
        }

        if (modal.parentElement !== document.body) {
            document.body.appendChild(modal);
        }

        const setAlert = (element, type, message) => {
            element.className = `payment-alert ${type === 'success' ? 'is-success' : 'is-error'}`;
            element.textContent = message;
            element.classList.remove('payment-hidden');
        };

        const clearAlert = (element) => {
            element.className = 'payment-alert payment-hidden';
            element.textContent = '';
        };

        const clearFieldErrors = () => {
            form.querySelectorAll('[data-payment-error]').forEach((el) => {
                el.textContent = '';
                el.classList.add('payment-hidden');
            });
            form.querySelectorAll('[data-payment-input]').forEach((el) => {
                el.classList.remove('payment-invalid');
                el.removeAttribute('aria-invalid');
            });
        };

        const showFieldError = (field, message) => {
            const input = form.querySelector(`[data-payment-input="${field}"]`);
            const error = form.querySelector(`[data-payment-error="${field}"]`);
            if (input) {
                input.classList.add('payment-invalid');
                input.setAttribute('aria-invalid', 'true');
            }
            if (error) {
                error.textContent = message;
                error.classList.remove('payment-hidden');
            }
        };

        const showModal = () => {
            modal.classList.add('is-open');
            modal.setAttribute('aria-hidden', 'false');
            document.body.style.overflow = 'hidden';
        };

        const closeModal = () => {
            modal.classList.remove('is-open');
            modal.setAttribute('aria-hidden', 'true');
            document.body.style.overflow = '';
        };

        const resetForm = () => {
            form.reset();
            clearFieldErrors();
            clearAlert(modalAlert);
            contextLabel.textContent = '';
        };

        const refreshModule = async () => {
            const response = await fetch(root.dataset.refreshUrl, {
                headers: { 'X-Requested-With': 'XMLHttpRequest' },
                credentials: 'same-origin'
            });

            if (!response.ok) {
                throw new Error('Unable to refresh billing records.');
            }

            const html = await response.text();
            const wrapper = document.createElement('div');
            wrapper.innerHTML = html.trim();
            const newRoot = wrapper.querySelector('.payment-module');
            if (!newRoot) {
                throw new Error('Billing markup was invalid.');
            }

            root.replaceWith(newRoot);
            if (typeof window.__initializeHomeownerBillingModule === 'function') {
                window.__initializeHomeownerBillingModule(newRoot);
            }
        };

        const parseError = async (response) => {
            const contentType = response.headers.get('content-type') || '';
            if (contentType.includes('application/json')) {
                return response.json();
            }

            return {
                success: false,
                message: (await response.text()) || 'Unable to submit payment.'
            };
        };

        const onRootClick = (event) => {
            const button = event.target.closest('[data-payment-action="submit"]');
            if (!button) {
                return;
            }

            resetForm();
            form.querySelector('[data-payment-input="BillingID"]').value = button.getAttribute('data-billing-id') || '';
            form.querySelector('[data-payment-input="SubmittedAmount"]').value = button.getAttribute('data-billing-amount') || '';
            contextLabel.textContent = `${button.getAttribute('data-billing-description') || 'Bill'} • Status: ${button.getAttribute('data-billing-status') || 'Pending'}`;
            showModal();
        };

        const onModalClick = (event) => {
            if (event.target === modal || event.target.closest('[data-payment-close]')) {
                closeModal();
            }
        };

        const onKeydown = (event) => {
            if (event.key === 'Escape' && modal.classList.contains('is-open')) {
                closeModal();
            }
        };

        const onSubmitClick = async () => {
            if (form.dataset.submitting === 'true' || !form.reportValidity()) {
                return;
            }

            form.dataset.submitting = 'true';
            submitButton.disabled = true;
            const originalMarkup = submitButton.innerHTML;
            submitButton.innerHTML = '<i class="fas fa-spinner fa-spin"></i><span>Submitting...</span>';
            clearAlert(modalAlert);
            clearAlert(sectionAlert);
            clearFieldErrors();

            try {
                const formData = new FormData(form);
                formData.append('__RequestVerificationToken', token);

                const response = await fetch(root.dataset.submitUrl, {
                    method: 'POST',
                    body: formData,
                    credentials: 'same-origin'
                });

                const payload = await parseError(response);
                if (!response.ok || !payload || payload.success === false) {
                    if (payload?.field && payload?.message) {
                        showFieldError(payload.field, payload.message);
                    }

                    if (payload?.validationErrors) {
                        Object.keys(payload.validationErrors).forEach((field) => {
                            const messages = payload.validationErrors[field];
                            if (Array.isArray(messages) && messages.length) {
                                showFieldError(field, messages[0]);
                            }
                        });
                    }

                    setAlert(modalAlert, 'error', payload?.message || 'Unable to submit payment.');
                    return;
                }

                closeModal();
                setAlert(sectionAlert, 'success', payload.message || 'Payment submitted successfully.');
                await refreshModule();
            } catch (error) {
                console.error(error);
                setAlert(modalAlert, 'error', error.message || 'Unable to submit payment.');
            } finally {
                delete form.dataset.submitting;
                submitButton.disabled = false;
                submitButton.innerHTML = originalMarkup;
            }
        };

        root.addEventListener('click', onRootClick);
        modal.addEventListener('click', onModalClick);
        document.addEventListener('keydown', onKeydown);
        submitButton.addEventListener('click', onSubmitClick);

        const cleanup = () => {
            root.removeEventListener('click', onRootClick);
            modal.removeEventListener('click', onModalClick);
            document.removeEventListener('keydown', onKeydown);
            submitButton.removeEventListener('click', onSubmitClick);
            closeModal();
            if (modal.parentElement === document.body) {
                modal.remove();
            }
            root.dataset.billingBound = 'false';
        };

        if (typeof window.__registerHomeownerEmbeddedCleanup === 'function') {
            window.__registerHomeownerEmbeddedCleanup(cleanup);
        }
    }

    window.__initializeHomeownerBillingModule = initializeBillingModule;
    window.homeownerDashboardModuleInitializers = window.homeownerDashboardModuleInitializers || {};
    window.homeownerDashboardModuleInitializers.billing = initializeBillingModule;
})();
