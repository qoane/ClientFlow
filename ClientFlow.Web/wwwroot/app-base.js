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
    });
})();
