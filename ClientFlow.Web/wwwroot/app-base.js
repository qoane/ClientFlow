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
