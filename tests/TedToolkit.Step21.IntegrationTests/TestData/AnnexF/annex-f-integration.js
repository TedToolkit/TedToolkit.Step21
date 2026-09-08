"use strict";

const assert = require("node:assert/strict");
const fs = require("node:fs");

const P21 = require(process.argv[2]);
const initial = JSON.parse(fs.readFileSync(process.argv[3], "utf8"));
let latest = initial;
let assertions = 0;

function check(action) {
    action();
    assertions += 1;
}

const host = {
    snapshot: () => initial,
    apply: state => { latest = state; },
    resolveEntity: name => ({ type: "entity", name }),
    resolveValue: name => ({ type: "value", name }),
    resolveConstantEntity: name => ({ type: "constant-entity", name }),
    resolveConstantValue: name => ({ type: "constant-value", name }),
    resolveURI: uri => ({ type: "uri", uri }),
    generateVerification: () => "AQID"
};

const model = new P21.Model(host);

check(() => assert.equal(P21.bridgeFormatVersion, 1));
check(() => assert.equal(Object.hasOwn(model, "integer"), true));
check(() => assert.equal(model.integer.$value instanceof P21.Integer, true));
check(() => assert.equal(model.integer.$value.valueOf(), 10));
check(() => assert.equal(model.integer.$value.toString(), "10"));
check(() => assert.equal(model.integer.$value.toP21String(), "10"));
check(() => assert.equal(model.real.$value.valueOf(), 1.25));
check(() => assert.equal(model.real.$value.toP21String().includes("."), true));
check(() => assert.equal(model.text.$value.valueOf(), "hé'llo\\"));
check(() => assert.equal(model.text.$value.toString(), "hé'llo\\"));
check(() => assert.match(model.text.$value.toP21String(), /^'.*'$/));
check(() => assert.equal(new P21.Enumeration(true).valueOf(), true));
check(() => assert.equal(new P21.Enumeration(false).valueOf(), false));
check(() => assert.equal(new P21.Enumeration("RED").valueOf(), "RED"));
check(() => assert.equal(new P21.Enumeration("RED").toP21String(), ".RED."));
check(() => assert.equal(model.binary.$value.toString(), "1A"));
check(() => assert.equal(model.binary.$value.toP21String(), "\"1A\""));
check(() => assert.deepEqual(model.eid.$value.valueOf(), { type: "entity", name: "20" }));
check(() => assert.equal(model.eid.$value.toP21String(), "#20"));
check(() => assert.deepEqual(model.vid.$value.valueOf(), { type: "value", name: "30" }));
check(() => assert.equal(model.vid.$value.toP21String(), "@30"));
check(() => assert.deepEqual(model.cin.$value.valueOf(), { type: "constant-entity", name: "INCH" }));
check(() => assert.equal(model.cin.$value.toP21String(), "#INCH"));
check(() => assert.deepEqual(model.cvn.$value.valueOf(), { type: "constant-value", name: "PI" }));
check(() => assert.equal(model.cvn.$value.toP21String(), "@PI"));
check(() => assert.equal(model["null-value"].$value, null));
check(() => assert.deepEqual(model.list.$value.valueOf(), [1, "member", null]));
check(() => assert.deepEqual(model.list.$value.toString(), ["1", "member", null]));
check(() => assert.deepEqual(model.list.$value.toP21String(), ["1", "'member'", "$" ]));
check(() => assert.deepEqual(model.resource.$value.valueOf(), { type: "uri", uri: "#integer" }));
check(() => assert.equal(model.resource.$value.toString(), "#integer"));
check(() => assert.equal(model.resource.$value.toP21String(), "<#integer>"));
check(() => assert.throws(() => new P21.URI("1:relative"), /invalid/));
check(() => assert.equal(new P21.URI("foo:?opaque").toP21String(), "<foo:?opaque>"));
check(() => assert.equal(model.tagged.$label.valueOf(), "original"));
check(() => assert.equal(new P21.Wrapper().toP21String(), "$"));
check(() => assert.equal(model.uri().toP21String(), "<https://example.test/original.step>"));
check(() => assert.equal(model.name().valueOf(), "annex-f.original"));

const initialPopulation = model.schema_population()[0];
check(() => assert.equal(initialPopulation.uri().toString(), "https://example.test/population.step"));
check(() => assert.equal(initialPopulation.stamp() instanceof Date, true));
check(() => assert.equal(initialPopulation.verification(), true));

model.integer.$value = new P21.Integer(42);
model.real.$value = new P21.Real(10);
model.text.$value = new P21.String("changed ' text");
model.enumeration.$value = new P21.Enumeration(false);
model.binary.$value = new P21.Binary("0F");
model.eid.$value = new P21.EID("21");
model.vid.$value = new P21.VID("31");
model.cin.$value = new P21.CIN("FOOT");
model.cvn.$value = new P21.CVN("TAU");
model["null-value"].$value = null;
model.list.$value = new P21.List(new P21.Integer(2), new P21.String("changed"), null);
model.resource.$value = new P21.URI("next.step#anchor");
model.tagged.$label = new P21.String("changed label");
model.set_uri(new P21.URI("https://example.test/changed.step"));
model.set_name(new P21.String("annex-f.changed"));

initialPopulation.set_uri(new P21.URI("https://example.test/changed-population.step"));
initialPopulation.set_stamp(new Date("2026-09-08T01:02:03.004Z"));
initialPopulation.set_verification();
const added = new P21.Population();
added.set_uri(new P21.URI("relative-population.step"));
added.set_stamp(new Date("2026-09-09T00:00:00.000Z"));
model.set_schema_population([initialPopulation, added]);
model.schema_population()[1].set_verification();

check(() => assert.equal(model.integer.$value.toP21String(), "42"));
check(() => assert.equal(model.real.$value.toP21String(), "10."));
check(() => assert.equal(model.enumeration.$value.toP21String(), ".F."));
check(() => assert.equal(model.schema_population().length, 2));
check(() => assert.equal(model.schema_population()[1].verification(), true));

fs.writeFileSync(process.argv[4], JSON.stringify(latest), "utf8");
process.stdout.write("ANNEX_F_NODE_OK assertions=" + assertions + "\n");
