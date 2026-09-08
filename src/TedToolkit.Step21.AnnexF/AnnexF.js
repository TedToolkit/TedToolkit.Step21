(function (root, factory) {
    "use strict";
    var api = factory();
    if (typeof module === "object" && module && module.exports) {
        module.exports = api;
    }
    root.P21 = api;
}(typeof globalThis === "object" ? globalThis : this, function () {
    "use strict";

    var bridgeFormatVersion = 1;

    function fail(message) {
        throw new TypeError(message);
    }

    function requireFiniteNumber(value, name) {
        if (typeof value !== "number" || !isFinite(value)) {
            fail(name + " must be a finite ECMAScript number.");
        }
        return value;
    }

    function expandInteger(value) {
        var text = String(value);
        var marker = text.search(/[eE]/);
        if (marker < 0) {
            return text;
        }
        var mantissa = text.substring(0, marker);
        var exponent = Number(text.substring(marker + 1));
        var negative = mantissa.charAt(0) === "-";
        if (negative) {
            mantissa = mantissa.substring(1);
        }
        var point = mantissa.indexOf(".");
        var digits = mantissa.replace(".", "");
        var decimal = point < 0 ? digits.length : point;
        var position = decimal + exponent;
        if (position <= 0) {
            digits = new Array(1 - position).join("0") + digits;
            position = 1;
        }
        if (position < digits.length) {
            fail("P21.Integer cannot represent a non-integral ECMAScript number.");
        }
        digits += new Array(position - digits.length + 1).join("0");
        return (negative ? "-" : "") + digits;
    }

    function realP21(value) {
        var text = String(value);
        var marker = text.search(/[eE]/);
        if (marker < 0) {
            return text.indexOf(".") < 0 ? text + "." : text;
        }
        var mantissa = text.substring(0, marker);
        if (mantissa.indexOf(".") < 0) {
            mantissa += ".";
        }
        var exponent = Number(text.substring(marker + 1));
        return mantissa + "E" + String(exponent);
    }

    function stringP21(value) {
        var result = "'";
        var index;
        for (index = 0; index < value.length; index += 1) {
            var code = value.charCodeAt(index);
            if (code === 39) {
                result += "''";
            } else if (code === 92) {
                result += "\\\\";
            } else if (code >= 32 && code <= 126) {
                result += value.charAt(index);
            } else if (code >= 0xD800 && code <= 0xDBFF && index + 1 < value.length) {
                var low = value.charCodeAt(index + 1);
                if (low < 0xDC00 || low > 0xDFFF) {
                    fail("P21.String contains an unpaired high surrogate.");
                }
                var scalar = 0x10000 + ((code - 0xD800) * 0x400) + low - 0xDC00;
                result += "\\X4\\" + scalar.toString(16).toUpperCase().padStart(8, "0") + "\\X0\\";
                index += 1;
            } else if (code >= 0xD800 && code <= 0xDFFF) {
                fail("P21.String contains an unpaired surrogate.");
            } else {
                result += "\\X2\\" + code.toString(16).toUpperCase().padStart(4, "0") + "\\X0\\";
            }
        }
        return result + "'";
    }

    function requireName(value, pattern, description) {
        if (typeof value !== "string" || !pattern.test(value)) {
            fail(description + " is invalid.");
        }
        return value;
    }

    function Wrapper() {
    }
    Wrapper.prototype.valueOf = function () {
        return null;
    };
    Wrapper.prototype.toString = function () {
        return String(this.valueOf());
    };
    Wrapper.prototype.toP21String = function () {
        return "$";
    };

    function inherit(type) {
        type.prototype = Object.create(Wrapper.prototype);
        type.prototype.constructor = type;
    }

    function Integer(value, p21) {
        this._value = requireFiniteNumber(Number(value), "P21.Integer value");
        if (Math.floor(this._value) !== this._value) {
            fail("P21.Integer value must be integral.");
        }
        this._p21 = p21 === undefined ? expandInteger(this._value) : String(p21);
        if (!/^-?(?:0|[1-9][0-9]*)$/.test(this._p21)) {
            fail("P21.Integer spelling is invalid.");
        }
    }
    inherit(Integer);
    Integer.prototype.valueOf = function () { return this._value; };
    Integer.prototype.toString = function () { return String(this._value); };
    Integer.prototype.toP21String = function () { return this._p21; };

    function Real(value, p21) {
        this._value = requireFiniteNumber(Number(value), "P21.Real value");
        this._p21 = p21 === undefined ? realP21(this._value) : String(p21);
        if (!/^-?[0-9]+\.[0-9]*(?:E[+-]?[0-9]+)?$/.test(this._p21)) {
            fail("P21.Real spelling is invalid.");
        }
    }
    inherit(Real);
    Real.prototype.valueOf = function () { return this._value; };
    Real.prototype.toString = function () { return String(this._value); };
    Real.prototype.toP21String = function () { return this._p21; };

    function P21String(value) {
        if (typeof value !== "string") {
            fail("P21.String value must be an ECMAScript string.");
        }
        this._value = value;
    }
    inherit(P21String);
    P21String.prototype.valueOf = function () { return this._value; };
    P21String.prototype.toString = function () { return this._value; };
    P21String.prototype.toP21String = function () { return stringP21(this._value); };

    function Enumeration(value) {
        if (value === true) {
            this._value = true;
            this._symbol = "T";
        } else if (value === false) {
            this._value = false;
            this._symbol = "F";
        } else {
            this._symbol = requireName(value, /^[A-Z_][A-Z0-9_]*$/, "P21.Enumeration value");
            this._value = this._symbol === "T" ? true : this._symbol === "F" ? false : this._symbol;
        }
    }
    inherit(Enumeration);
    Enumeration.prototype.valueOf = function () { return this._value; };
    Enumeration.prototype.toString = function () { return String(this._value); };
    Enumeration.prototype.toP21String = function () { return "." + this._symbol + "."; };

    function Binary(value) {
        this._value = requireName(value, /^[0-3][0-9A-F]*$/, "P21.Binary value");
        if (Number(this._value.charAt(0)) > (this._value.length - 1) * 4) {
            fail("P21.Binary unused-bit count exceeds the encoded bit count.");
        }
    }
    inherit(Binary);
    Binary.prototype.valueOf = function () { return this._value; };
    Binary.prototype.toString = function () { return this._value; };
    Binary.prototype.toP21String = function () { return "\"" + this._value + "\""; };

    function referenceType(prefix, resolverName, constant) {
        function Reference(value, host) {
            this._name = requireName(
                value,
                constant ? /^[A-Z_][A-Z0-9_]*$/ : /^[1-9][0-9]*$/,
                "P21 reference name");
            this._host = host || null;
        }
        inherit(Reference);
        Reference.prototype.valueOf = function () {
            return this._host && typeof this._host[resolverName] === "function"
                ? this._host[resolverName](this._name)
                : null;
        };
        Reference.prototype.toString = function () { return this._name; };
        Reference.prototype.toP21String = function () { return prefix + this._name; };
        return Reference;
    }

    var EID = referenceType("#", "resolveEntity", false);
    var VID = referenceType("@", "resolveValue", false);
    var CIN = referenceType("#", "resolveConstantEntity", true);
    var CVN = referenceType("@", "resolveConstantValue", true);

    function List() {
        this._values = Array.prototype.slice.call(arguments);
        this._values.forEach(function (value) {
            if (value !== null && !(value instanceof Wrapper)) {
                fail("P21.List members must be P21.Wrapper objects or null.");
            }
        });
    }
    inherit(List);
    List.prototype.valueOf = function () {
        return this._values.map(function (value) { return value === null ? null : value.valueOf(); });
    };
    List.prototype.toString = function () {
        return this._values.map(function (value) { return value === null ? null : value.toString(); });
    };
    List.prototype.toP21String = function () {
        return this._values.map(function (value) { return value === null ? "$" : value.toP21String(); });
    };

    function isUriScheme(value) {
        return /^[A-Za-z][A-Za-z0-9+.-]*$/.test(value);
    }

    function validURI(value) {
        if (typeof value !== "string" || value.length === 0 || /[<>\s]/.test(value)) {
            return false;
        }
        var hash = value.indexOf("#");
        if (hash >= 0 && value.indexOf("#", hash + 1) >= 0) {
            return false;
        }
        var allowed = /^[A-Za-z0-9\-_.!~*'();/?:@&=+$,#%]+$/;
        if (!allowed.test(value)) {
            return false;
        }
        var index;
        for (index = 0; index < value.length; index += 1) {
            if (value.charAt(index) === "%") {
                if (!/^[0-9A-Fa-f]{2}$/.test(value.substring(index + 1, index + 3))) {
                    return false;
                }
                index += 2;
            }
        }

        if (hash === 0) {
            return true;
        }

        var resourceLength = hash >= 0 ? hash : value.length;
        var resource = value.substring(0, resourceLength);
        var colon = resource.indexOf(":");
        var slash = resource.indexOf("/");
        if (colon > 0 && (slash < 0 || colon < slash) && isUriScheme(resource.substring(0, colon))) {
            return colon + 1 < resourceLength;
        }

        var query = resource.indexOf("?");
        var path = query >= 0 ? resource.substring(0, query) : resource;
        if (path.length === 0) {
            return false;
        }
        var pathSlash = path.indexOf("/");
        var firstSegment = pathSlash >= 0 ? path.substring(0, pathSlash) : path;
        if (firstSegment.length === 0 && path.charAt(0) !== "/") {
            return false;
        }
        if (firstSegment.indexOf(":") >= 0) {
            return false;
        }
        return true;
    }

    function URI(value, host) {
        if (!validURI(value)) {
            fail("P21.URI value is invalid.");
        }
        this._value = value;
        this._host = host || null;
    }
    inherit(URI);
    URI.prototype.valueOf = function () {
        return this._host && typeof this._host.resolveURI === "function"
            ? this._host.resolveURI(this._value)
            : null;
    };
    URI.prototype.toString = function () { return this._value; };
    URI.prototype.toP21String = function () { return "<" + this._value + ">"; };

    function fromNode(node, host) {
        if (!node || typeof node !== "object") {
            fail("Annex F bridge values must be objects.");
        }
        switch (node.kind) {
        case "null": return null;
        case "integer": return new Integer(Number(node.p21), node.p21);
        case "real": return new Real(Number(node.p21.replace("E", "e")), node.p21);
        case "string": return new P21String(node.value);
        case "enumeration": return new Enumeration(node.value);
        case "binary": return new Binary(node.value);
        case "eid": return new EID(node.name, host);
        case "vid": return new VID(node.name, host);
        case "cin": return new CIN(node.name, host);
        case "cvn": return new CVN(node.name, host);
        case "list":
            return new (Function.prototype.bind.apply(List, [null].concat(node.values.map(function (value) {
                return fromNode(value, host);
            }))));
        case "uri": return new URI(node.value, host);
        default: fail("Unknown Annex F bridge value kind '" + node.kind + "'.");
        }
    }

    function toNode(value) {
        if (value === null) { return { kind: "null" }; }
        if (!(value instanceof Wrapper)) { fail("Annex F values must be P21.Wrapper objects or null."); }
        if (value instanceof Integer) { return { kind: "integer", p21: value.toP21String() }; }
        if (value instanceof Real) { return { kind: "real", p21: value.toP21String() }; }
        if (value instanceof P21String) { return { kind: "string", value: value.valueOf() }; }
        if (value instanceof Enumeration) { return { kind: "enumeration", value: value._symbol }; }
        if (value instanceof Binary) { return { kind: "binary", value: value.valueOf() }; }
        if (value instanceof EID) { return { kind: "eid", name: value.toString() }; }
        if (value instanceof VID) { return { kind: "vid", name: value.toString() }; }
        if (value instanceof CIN) { return { kind: "cin", name: value.toString() }; }
        if (value instanceof CVN) { return { kind: "cvn", name: value.toString() }; }
        if (value instanceof List) { return { kind: "list", values: value._values.map(toNode) }; }
        if (value instanceof URI) { return { kind: "uri", value: value.toString() }; }
        fail("Unknown P21.Wrapper subtype.");
    }

    function copyState(state) {
        return JSON.parse(JSON.stringify(state));
    }

    function commit(model) {
        if (typeof model._host.apply === "function") {
            model._host.apply(copyState(model._state));
        }
    }

    function Anchor(model, state) {
        var view = this;
        Object.defineProperty(view, "$value", {
            enumerable: true,
            configurable: false,
            get: function () { return fromNode(state.value, model._host); },
            set: function (value) { state.value = toNode(value); commit(model); }
        });
        state.tags.forEach(function (tag) {
            Object.defineProperty(view, "$" + tag.name, {
                enumerable: true,
                configurable: false,
                get: function () { return fromNode(tag.value, model._host); },
                set: function (value) { tag.value = toNode(value); commit(model); }
            });
        });
    }

    function Population(state, model) {
        this._state = state || { uri: "#", stamp: null, messageDigest: null, verification: false };
        this._model = model || null;
    }
    Population.prototype.uri = function () { return new URI(this._state.uri, this._model && this._model._host); };
    Population.prototype.stamp = function () { return this._state.stamp === null ? null : new Date(this._state.stamp); };
    Population.prototype.verification = function () { return this._state.verification === true; };
    Population.prototype.set_uri = function (value) {
        if (!(value instanceof URI)) { fail("P21.Population.set_uri requires a P21.URI."); }
        this._state.uri = value.toString();
        if (this._model) { commit(this._model); }
    };
    Population.prototype.set_stamp = function (value) {
        if (!(value instanceof Date) || isNaN(value.valueOf())) {
            fail("P21.Population.set_stamp requires a valid ECMAScript Date.");
        }
        this._state.stamp = value.toISOString();
        if (this._model) { commit(this._model); }
    };
    Population.prototype.set_verification = function () {
        if (!this._model || typeof this._model._host.generateVerification !== "function") {
            fail("P21.Population.set_verification requires the caller-supplied generateVerification capability.");
        }
        var digest = this._model._host.generateVerification(this);
        if (typeof digest !== "string" || digest.length === 0
                || !/^(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=)?$/.test(digest)) {
            fail("The generated schema-population verification must be canonical Base64.");
        }
        this._state.messageDigest = digest;
        this._state.verification = true;
        commit(this._model);
    };

    function Model(host) {
        if (!host || typeof host.snapshot !== "function") {
            fail("P21.Model requires a caller-supplied host with snapshot().");
        }
        var state = host.snapshot();
        if (!state || state.formatVersion !== bridgeFormatVersion || !Array.isArray(state.anchors)
                || !Array.isArray(state.schemaPopulation)) {
            fail("The caller-supplied Annex F bridge snapshot is invalid or unsupported.");
        }
        this._host = host;
        this._state = copyState(state);
        this._populations = this._state.schemaPopulation.map(function (population) {
            return new Population(population, this);
        }, this);
        this._state.anchors.forEach(function (anchor) {
            Object.defineProperty(this, anchor.name, {
                enumerable: true,
                configurable: false,
                writable: false,
                value: new Anchor(this, anchor)
            });
        }, this);
    }
    Model.prototype.uri = function () { return new URI(this._state.uri, this._host); };
    Model.prototype.name = function () { return new P21String(this._state.name); };
    Model.prototype.schema_population = function () { return this._populations.slice(); };
    Model.prototype.set_uri = function (value) {
        if (!(value instanceof URI)) { fail("P21.Model.set_uri requires a P21.URI."); }
        this._state.uri = value.toString();
        commit(this);
    };
    Model.prototype.set_name = function (value) {
        if (!(value instanceof P21String)) { fail("P21.Model.set_name requires a P21.String."); }
        this._state.name = value.valueOf();
        commit(this);
    };
    Model.prototype.set_schema_population = function (values) {
        if (!Array.isArray(values) || values.some(function (value) { return !(value instanceof Population); })) {
            fail("P21.Model.set_schema_population requires an array of P21.Population objects.");
        }
        this._state.schemaPopulation = values.map(function (value) { return copyState(value._state); });
        this._populations = this._state.schemaPopulation.map(function (population) {
            return new Population(population, this);
        }, this);
        commit(this);
    };

    return Object.freeze({
        bridgeFormatVersion: bridgeFormatVersion,
        Wrapper: Wrapper,
        Integer: Integer,
        Real: Real,
        String: P21String,
        Enumeration: Enumeration,
        Binary: Binary,
        EID: EID,
        VID: VID,
        CIN: CIN,
        CVN: CVN,
        List: List,
        URI: URI,
        Population: Population,
        Model: Model
    });
}));
