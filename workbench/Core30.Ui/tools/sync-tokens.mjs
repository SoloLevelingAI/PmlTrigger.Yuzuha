import { readFileSync, writeFileSync } from "node:fs";

const args = process.argv.slice(2);
const check = args[0] === "--check";
if (check) args.shift();
if (args.length > 1) throw new Error("Usage: node sync-tokens.mjs [--check] [Penpot-export.json]");

const snapshot = new URL("../Design/Core30-Light.tokens.json", import.meta.url);
const destination = new URL("../Themes/Tokens.xaml", import.meta.url);
const sourceText = readFileSync(args[0] ?? snapshot, "utf8");
const source = JSON.parse(sourceText.replace(/^\uFEFF/, ""));
const { tokenSetOrder, activeSets } = source.$metadata;
const tokens = new Map();

// Penpot applies later sets in tokenSetOrder on top of earlier sets.
for (const set of tokenSetOrder.filter(name => activeSets.includes(name))) {
  for (const [group, entries] of Object.entries(source[set])) {
    for (const [name, token] of Object.entries(entries)) tokens.set(group + "." + name, token);
  }
}
if (tokens.size === 0) throw new Error("The Penpot export has no active tokens.");

function resolve(key, chain = []) {
  if (!tokens.has(key) || chain.includes(key)) {
    throw new Error("Invalid token reference: " + [...chain, key].join(" → "));
  }
  const value = tokens.get(key).$value;
  const alias = typeof value === "string" ? /^\{(.+)\}$/.exec(value) : null;
  return alias ? resolve(alias[1], [...chain, key]) : value;
}

function dip(value) {
  const match = /^(-?\d+(?:\.\d+)?)(px|rem)$/.exec(String(value));
  if (!match) throw new Error("Unsupported Penpot dimension: " + value);
  return Number(match[1]) * (match[2] === "rem" ? 16 : 1);
}

function rgba(value) {
  if (/^#[\da-f]{6}$/i.test(value)) return { hex: value, alpha: 1 };
  const match = /^rgba\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*([\d.]+)\s*\)$/.exec(value);
  if (!match) throw new Error("Unsupported Penpot color: " + value);
  const hex = "#" + match.slice(1, 4).map(channel => Number(channel).toString(16).padStart(2, "0")).join("");
  return { hex, alpha: Number(match[4]) };
}

const escape = value => String(value).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/"/g, "&quot;");
const element = (type, key, value) => `  <${type} x:Key="${escape(key)}">${escape(value)}</${type}>`;
const embedded = new Set(["Noto Sans CJK SC", "Roboto Mono"]);
const xaml = [
  "<!-- Generated from Design/Core30-Light.tokens.json by tools/sync-tokens.mjs. -->",
  '<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"',
  '                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"',
  '                    xmlns:sys="clr-namespace:System;assembly=mscorlib">'
];

for (const [key, token] of tokens) {
  const [group] = key.split(".");
  const value = resolve(key);
  switch (token.$type) {
    case "color": {
      const { hex, alpha } = rgba(value);
      const color = alpha === 1 ? hex : "#" + Math.round(alpha * 255).toString(16).padStart(2, "0") + hex.slice(1);
      xaml.push(group === "palette"
        ? element("Color", key, color)
        : `  <SolidColorBrush x:Key="${escape(key)}" Color="${color}" />`);
      break;
    }
    case "borderRadius":
      xaml.push(element("CornerRadius", key, dip(value)));
      break;
    case "spacing":
    case "dimension":
    case "fontSizes":
      xaml.push(element("sys:Double", key, dip(value)));
      break;
    case "fontFamilies": {
      const families = Array.isArray(value) ? value : [value];
      xaml.push(element("FontFamily", key, families.map(name =>
        embedded.has(name) ? "/Core30.Ui;component/Fonts/#" + name : name).join(", ")));
      break;
    }
    case "fontWeights":
      xaml.push(element("FontWeight", key, value));
      break;
    case "shadow": {
      const [shadow] = value;
      if (value.length !== 1 || shadow.inset || dip(shadow.spread) !== 0) {
        throw new Error("WPF requires one outer shadow without spread: " + key);
      }
      const x = dip(shadow.offsetX);
      const y = dip(shadow.offsetY);
      const direction = (360 - Math.atan2(y, x) * 180 / Math.PI) % 360;
      const { hex, alpha } = rgba(shadow.color);
      xaml.push(`  <DropShadowEffect x:Key="${escape(key)}" Color="${hex}" Opacity="${alpha}"` +
        ` BlurRadius="${dip(shadow.blur)}" Direction="${direction}" ShadowDepth="${Math.hypot(x, y)}" RenderingBias="Quality" />`);
      break;
    }
    default:
      throw new Error("Unsupported Penpot token type: " + key + " (" + token.$type + ")");
  }
}

const space = name => dip(resolve("space." + name));
xaml.push(element("CornerRadius", "radius.pill.badge", Math.min(dip(resolve("radius.pill")), 13)));
// WindowChrome passes this value to CreateRoundRectRgn as the ellipse diameter.
xaml.push(element("CornerRadius", "radius.window-region", 2 * dip(resolve("radius.card"))));
for (const [key, sides] of Object.entries({
  "inset.button": ["md", "sm", "md", "sm"],
  "inset.card": ["2xl", "xl", "2xl", "xl"],
  "inset.compact-card": ["2xl", "lg", "2xl", "lg"],
  "inset.dense-card": ["2xl", "md", "2xl", "md"],
  "inset.hero": ["3xl", "2xl", "3xl", "2xl"],
  "inset.compact-hero": ["3xl", "md", "3xl", "md"],
  "inset.content": ["4xl", "xl", "4xl", "xl"],
  "inset.compact-content": ["3xl", "lg", "3xl", "md"]
})) xaml.push(element("Thickness", key, sides.map(space).join(",")));

const generated = xaml.concat("</ResourceDictionary>", "").join("\n");
if (check) {
  if (readFileSync(snapshot, "utf8") !== sourceText || readFileSync(destination, "utf8").replace(/\r\n/g, "\n") !== generated) {
    throw new Error("The bundled Penpot export or the generated WPF tokens are out of date.");
  }
} else {
  if (args[0]) writeFileSync(snapshot, sourceText);
  writeFileSync(destination, generated);
}
console.log(`${tokens.size} active Penpot tokens (${check ? "verified" : "generated"}).`);
