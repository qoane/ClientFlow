(function () {
    if (window.__appBaseLoaded) return;
    window.__appBaseLoaded = true;

    function detectBasePath() {
        var path = window.location.pathname || '/';
        if (!path || path === '/') {
            return '/';
        }
        var segments = path.split('/').filter(function (seg) { return seg.length > 0; });
        if (segments.length === 0) {
            return '/';
        }
        var markers = ['admin', 'kiosk', 'surveys', 'dashboard', 'flow'];
        var cutIndex = segments.length;
        for (var i = 0; i < segments.length; i++) {
            var seg = segments[i];
            var lower = seg.toLowerCase();
            if (seg.indexOf('.') !== -1 || markers.indexOf(lower) !== -1) {
                cutIndex = i;
                break;
            }
        }
        var baseSegments = segments.slice(0, cutIndex);
        var base = '/' + baseSegments.join('/');
        if (base !== '/' && !base.endsWith('/')) {
            base += '/';
        }
        if (!base) base = '/';
        return base;
    }

    var basePath = detectBasePath();
    window.__appBasePath = basePath;

    function normalise(path) {
        if (!path) return basePath;
        if (/^https?:/i.test(path)) return path;
        if (path.startsWith('/')) {
            path = path.slice(1);
        }
        return basePath + path;
    }

    window.appBuildUrl = function (path) {
        return normalise(path || '');
    };

    window.appRedirect = function (path) {
        window.location.href = appBuildUrl(path || '');
    };

    window.appApiUrl = function (path) {
        if (!path) return normalise('api/');
        if (path.startsWith('/')) {
            path = path.slice(1);
        }
        return normalise(path);
    };

    var uiState = {
        styleInjected: false,
        toastContainer: null,
        modalBackdrop: null,
        modalResolve: null,
        spinnerOverlay: null,
        spinnerCount: 0
    };

    function ensureUiStyle() {
        if (uiState.styleInjected) return;
        uiState.styleInjected = true;
        var style = document.createElement('style');
        style.id = 'app-shared-ui';
        style.textContent = "\n            .app-toast-container{position:fixed;top:24px;right:24px;display:flex;flex-direction:column;gap:12px;z-index:2147483646;}\n            .app-toast{background:rgba(11,16,32,.95);color:#eef1ff;padding:12px 16px;border-radius:12px;box-shadow:0 12px 30px rgba(0,0,0,.35);border-left:4px solid #6cf0c2;opacity:0;transform:translateY(-10px);transition:opacity .18s ease,transform .18s ease;font-family:system-ui,-apple-system,'Segoe UI',Roboto,sans-serif;max-width:320px;line-height:1.4;}\n            .app-toast.show{opacity:1;transform:translateY(0);}\n            .app-toast[data-type="danger"]{border-left-color:#ff6b6b;}\n            .app-toast[data-type="warning"]{border-left-color:#ffd166;}\n            .app-modal-backdrop{position:fixed;inset:0;background:rgba(9,12,24,.78);display:flex;align-items:center;justify-content:center;z-index:2147483645;opacity:0;pointer-events:none;transition:opacity .2s ease;font-family:system-ui,-apple-system,'Segoe UI',Roboto,sans-serif;}\n            .app-modal-backdrop.visible{opacity:1;pointer-events:auto;}\n            .app-modal{background:linear-gradient(160deg,rgba(20,28,68,.98),rgba(15,22,54,.98));border:1px solid rgba(255,255,255,.08);border-radius:16px;min-width:280px;max-width:420px;padding:24px;box-shadow:0 20px 40px rgba(3,8,29,.55);color:#f7f9ff;}\n            .app-modal h2{margin:0 0 12px;font-size:18px;font-weight:600;}\n            .app-modal p{margin:0 0 18px;font-size:15px;line-height:1.5;color:#d0d6ff;}\n            .app-modal .app-modal-input{width:100%;padding:10px 12px;border-radius:10px;border:1px solid rgba(255,255,255,.12);background:rgba(7,11,28,.9);color:#f7f9ff;font-size:15px;margin-bottom:20px;}\n            .app-modal .app-modal-actions{display:flex;justify-content:flex-end;gap:12px;}\n            .app-modal button{border:none;border-radius:999px;padding:10px 18px;font-weight:600;cursor:pointer;font-size:14px;}\n            .app-modal button[data-role="secondary"]{background:rgba(255,255,255,.08);color:#eef1ff;}\n            .app-modal button[data-role="primary"]{background:#6cf0c2;color:#05291f;box-shadow:0 6px 18px rgba(108,240,194,.35);}\n            .app-spinner-overlay{position:fixed;inset:0;background:rgba(6,9,22,.72);display:flex;flex-direction:column;align-items:center;justify-content:center;z-index:2147483644;opacity:0;pointer-events:none;transition:opacity .2s ease;font-family:system-ui,-apple-system,'Segoe UI',Roboto,sans-serif;}\n            .app-spinner-overlay.visible{opacity:1;pointer-events:auto;}\n            .app-spinner{width:54px;height:54px;border:4px solid rgba(255,255,255,.18);border-top-color:#6cf0c2;border-radius:50%;animation:app-spin 1s linear infinite;}\n            .app-spinner-overlay p{margin-top:18px;color:#eef1ff;font-size:15px;letter-spacing:.02em;}\n            @keyframes app-spin{to{transform:rotate(360deg);}}\n        ";
        document.head.appendChild(style);
    }

    function ensureToastContainer() {
        ensureUiStyle();
        if (!uiState.toastContainer) {
            var div = document.createElement('div');
            div.className = 'app-toast-container';
            document.body.appendChild(div);
            uiState.toastContainer = div;
        }
        return uiState.toastContainer;
    }

    function ensureModalBackdrop() {
        ensureUiStyle();
        if (!uiState.modalBackdrop) {
            var backdrop = document.createElement('div');
            backdrop.className = 'app-modal-backdrop';
            backdrop.innerHTML = '<div class="app-modal"><h2></h2><p></p><input class="app-modal-input" style="display:none" /><div class="app-modal-actions"></div></div>';
            document.body.appendChild(backdrop);
            uiState.modalBackdrop = backdrop;
        }
        return uiState.modalBackdrop;
    }

    function showModal(options) {
        return new Promise(function (resolve) {
            ensureModalBackdrop();
            var backdrop = uiState.modalBackdrop;
            var modal = backdrop.querySelector('.app-modal');
            var titleEl = modal.querySelector('h2');
            var messageEl = modal.querySelector('p');
            var inputEl = modal.querySelector('.app-modal-input');
            var actionsEl = modal.querySelector('.app-modal-actions');

            titleEl.textContent = options.title || 'Notice';
            messageEl.textContent = options.message || '';
            inputEl.style.display = options.prompt ? '' : 'none';
            if (options.prompt) {
                inputEl.value = options.defaultValue || '';
                inputEl.placeholder = options.placeholder || '';
                setTimeout(function () { inputEl.focus(); inputEl.select(); }, 20);
            }

            actionsEl.innerHTML = '';
            var cancelText = options.cancelText;
            if (cancelText) {
                var cancelBtn = document.createElement('button');
                cancelBtn.type = 'button';
                cancelBtn.dataset.role = 'secondary';
                cancelBtn.textContent = cancelText;
                cancelBtn.onclick = function () {
                    hideModal();
                    resolve({ confirmed: false, value: null });
                };
                actionsEl.appendChild(cancelBtn);
            }

            var confirmBtn = document.createElement('button');
            confirmBtn.type = 'button';
            confirmBtn.dataset.role = 'primary';
            confirmBtn.textContent = options.confirmText || 'OK';
            confirmBtn.onclick = function () {
                hideModal();
                resolve({ confirmed: true, value: options.prompt ? inputEl.value : null });
            };
            actionsEl.appendChild(confirmBtn);

            function hideOnEscape(ev) {
                if (ev.key === 'Escape') {
                    ev.preventDefault();
                    document.removeEventListener('keydown', hideOnEscape);
                    hideModal();
                    resolve({ confirmed: false, value: null });
                }
                if (ev.key === 'Enter' && options.prompt) {
                    ev.preventDefault();
                    document.removeEventListener('keydown', hideOnEscape);
                    hideModal();
                    resolve({ confirmed: true, value: inputEl.value });
                }
            }

            function hideModal() {
                backdrop.classList.remove('visible');
                uiState.modalResolve = null;
                document.removeEventListener('keydown', hideOnEscape);
            }

            document.addEventListener('keydown', hideOnEscape);
            backdrop.classList.add('visible');
            uiState.modalResolve = resolve;
        });
    }

    window.appAlert = function (message, options) {
        options = options || {};
        options.message = message;
        options.title = options.title || 'Notice';
        options.confirmText = options.confirmText || 'OK';
        return showModal(options).then(function () { return; });
    };

    window.appConfirm = function (message, options) {
        options = options || {};
        options.message = message;
        options.title = options.title || 'Please Confirm';
        options.cancelText = options.cancelText || 'Cancel';
        options.confirmText = options.confirmText || 'Confirm';
        return showModal(options).then(function (result) { return !!result.confirmed; });
    };

    window.appPrompt = function (message, options) {
        options = options || {};
        options.message = message;
        options.prompt = true;
        options.title = options.title || 'Input Required';
        options.cancelText = options.cancelText || 'Cancel';
        options.confirmText = options.confirmText || 'Submit';
        return showModal(options).then(function (result) {
            if (!result.confirmed) return null;
            return result.value;
        });
    };

    window.appNotify = function (message, options) {
        options = options || {};
        var container = ensureToastContainer();
        var toast = document.createElement('div');
        toast.className = 'app-toast';
        if (options.type) {
            toast.dataset.type = options.type;
        }
        toast.textContent = message;
        container.appendChild(toast);
        requestAnimationFrame(function () {
            toast.classList.add('show');
        });
        var duration = typeof options.duration === 'number' ? options.duration : 3200;
        setTimeout(function () {
            toast.classList.remove('show');
            setTimeout(function () {
                toast.remove();
            }, 200);
        }, duration);
        return toast;
    };

    function ensureSpinnerOverlay() {
        ensureUiStyle();
        if (!uiState.spinnerOverlay) {
            var overlay = document.createElement('div');
            overlay.className = 'app-spinner-overlay';
            overlay.innerHTML = '<div class="app-spinner"></div><p>Working…</p>';
            document.body.appendChild(overlay);
            uiState.spinnerOverlay = overlay;
        }
        return uiState.spinnerOverlay;
    }

    function showSpinner(message) {
        var overlay = ensureSpinnerOverlay();
        uiState.spinnerCount++;
        if (message) {
            overlay.querySelector('p').textContent = message;
        } else {
            overlay.querySelector('p').textContent = 'Working…';
        }
        overlay.classList.add('visible');
    }

    function hideSpinner() {
        uiState.spinnerCount = Math.max(0, uiState.spinnerCount - 1);
        if (uiState.spinnerCount === 0 && uiState.spinnerOverlay) {
            uiState.spinnerOverlay.classList.remove('visible');
        }
    }

    window.appShowSpinner = function (message) {
        showSpinner(message);
    };

    window.appHideSpinner = function () {
        hideSpinner();
    };

    window.appWithSpinner = function (input, message) {
        showSpinner(message);
        var promise;
        if (typeof input === 'function') {
            try {
                promise = Promise.resolve().then(input);
            } catch (err) {
                hideSpinner();
                return Promise.reject(err);
            }
        } else {
            promise = Promise.resolve(input);
        }
        return promise.finally(function () {
            hideSpinner();
        });
    };

    window.appApiFetch = function (path, options) {
        var url = path;
        if (!/^https?:/i.test(path)) {
            url = appApiUrl(path);
        }
        return fetch(url, options);
    };

    function safeGetLocalStorage(key) {
        try {
            if (typeof window === 'undefined' || !window.localStorage) {
                return null;
            }
            return window.localStorage.getItem(key);
        } catch (err) {
            return null;
        }
    }

    function getCurrentRole() {
        return safeGetLocalStorage('userRole');
    }

    function hasRequiredRole(requiredRoles) {
        var role = getCurrentRole();
        if (!role) return false;
        if (!requiredRoles || requiredRoles.length === 0) return true;
        for (var i = 0; i < requiredRoles.length; i++) {
            if (requiredRoles[i] === role) {
                return true;
            }
        }
        return false;
    }

    window.appCurrentUserRole = function () {
        return getCurrentRole();
    };

    window.appRequireRole = function (requiredRoleOrRoles, options) {
        var roles = [];
        if (Array.isArray(requiredRoleOrRoles)) {
            roles = requiredRoleOrRoles.filter(function (r) { return typeof r === 'string' && r.length > 0; });
        } else if (typeof requiredRoleOrRoles === 'string' && requiredRoleOrRoles.length > 0) {
            roles = [requiredRoleOrRoles];
        }

        var token = safeGetLocalStorage('authToken');
        if (!token) {
            window.location.href = appBuildUrl('login.html');
            return false;
        }

        if (hasRequiredRole(roles)) {
            return true;
        }

        var redirectTo = options && options.redirectTo;
        if (!redirectTo) {
            redirectTo = 'admin/dashboard.html';
        }
        window.location.href = appBuildUrl(redirectTo);
        return false;
    };

    if (typeof window !== 'undefined' && typeof window.fetch === 'function' && !window.__appFetchWrapped) {
        window.__appFetchWrapped = true;
        var originalFetch = window.fetch.bind(window);
        window.fetch = function (resource, init) {
            if (typeof resource === 'string') {
                if (resource.startsWith('//')) {
                    return originalFetch(resource, init);
                }
                if (resource.startsWith('/')) {
                    resource = appBuildUrl(resource.slice(1));
                }
            } else if (resource && typeof resource === 'object' && typeof resource.url === 'string' && resource.url.startsWith(window.location.origin + '/')) {
                var relative = resource.url.substring((window.location.origin + '/').length);
                resource = new Request(appBuildUrl(relative), resource);
            }
            return originalFetch(resource, init);
        };
    }

    document.addEventListener('DOMContentLoaded', function () {
        try {
            var mustChange = window.localStorage && window.localStorage.getItem('mustChangePassword') === '1';
            if (mustChange) {
                var path = (window.location.pathname || '').toLowerCase();
                if (path.indexOf('/admin/') !== -1 && path.indexOf('change-password.html') === -1) {
                    window.location.href = appBuildUrl('admin/change-password.html');
                    return;
                }
            }
        } catch (err) {
            // Access to localStorage may fail in some environments; ignore and continue.
        }
        var adjustAttr = function (selector, attribute) {
            document.querySelectorAll(selector).forEach(function (el) {
                var value = el.getAttribute(attribute);
                if (value && value.startsWith('/') && !value.startsWith('//')) {
                    el.setAttribute(attribute, appBuildUrl(value.slice(1)));
                }
            });
        };
        adjustAttr('a[href^="/"]', 'href');
        adjustAttr('img[src^="/"]', 'src');
        adjustAttr('link[href^="/"]', 'href');
        adjustAttr('script[src^="/"]', 'src');
        adjustAttr('form[action^="/"]', 'action');

        var role = getCurrentRole();
        document.querySelectorAll('[data-require-role]').forEach(function (el) {
            var attr = el.getAttribute('data-require-role') || '';
            var required = attr.split(',').map(function (r) { return r.trim(); }).filter(function (r) { return r.length > 0; });
            if (!hasRequiredRole(required)) {
                el.remove();
            }
        });
    });
})();
