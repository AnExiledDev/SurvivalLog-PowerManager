// Just enough of Root.html and the game's HUD page for page.js to run in Jint: the root document with its page
// iframes, the vuplex bridge, clicks that bubble, and the rule the game's click-through uses to decide what on a page
// is clickable. Test code only; nothing here ships.
var window = this;

var location = { protocol: 'https:' };

var intervals = [];

function setInterval(callback) {
  intervals.push(callback);

  return intervals.length;
}

// One run of every interval the script set, which is how its 400 ms page check runs.
function poll() {
  intervals.slice().forEach(function (callback) { callback(); });
}

// Held and never fired: the tests answer every press themselves, as the plugin does.
var timers = {};
var nextTimer = 1;

function setTimeout(callback) {
  timers[nextTimer] = callback;

  return nextTimer++;
}

function clearTimeout(id) {
  delete timers[id];
}

var FakeElement = class {
  constructor(tagName) {
    var element = this;

    this.tagName = tagName;
    this.id = '';
    this.parentElement = null;
    this.children = [];
    this.text = '';
    this.attrs = {};
    this.classes = [];
    this.listeners = {};
    this.style = {};
    this.classList = {
      contains: function (name) { return element.classes.indexOf(name) >= 0; }
    };
  }

  get parentNode() { return this.parentElement; }

  // As in a browser: in its document, through its parents up to that document's head or body.
  get isConnected() {
    for (var element = this; element; element = element.parentElement) {
      if (element.isRoot) return true;
    }

    return false;
  }

  get firstChild() { return this.children.length ? this.children[0] : null; }

  get className() { return this.classes.join(' '); }

  set className(value) { this.classes = String(value).split(' ').filter(function (name) { return name !== ''; }); }

  // As in a browser: an element's text is all of its descendants' text, and setting it replaces the children.
  get textContent() {
    return this.text + this.children.map(function (child) { return child.textContent; }).join('');
  }

  set textContent(value) {
    this.children.slice().forEach(function (child) { this.removeChild(child); }, this);
    this.text = value == null ? '' : String(value);
  }

  hasAttribute(name) { return Object.prototype.hasOwnProperty.call(this.attrs, name); }

  getAttribute(name) { return this.hasAttribute(name) ? this.attrs[name] : null; }

  setAttribute(name, value) { this.attrs[name] = String(value); }

  addEventListener(type, listener) {
    (this.listeners[type] = this.listeners[type] || []).push(listener);
  }

  // A fragment's children move into the parent, as in a browser.
  appendChild(child) {
    if (child.tagName === '#fragment') {
      child.children.slice().forEach(function (moved) { this.appendChild(moved); }, this);

      return child;
    }

    if (child.parentElement) child.parentElement.removeChild(child);

    child.parentElement = this;
    this.children.push(child);

    return child;
  }

  insertBefore(child, before) {
    if (!before) return this.appendChild(child);

    if (child.parentElement) child.parentElement.removeChild(child);

    child.parentElement = this;
    this.children.splice(this.children.indexOf(before), 0, child);

    return child;
  }

  replaceChild(child, old) {
    if (child.parentElement) child.parentElement.removeChild(child);

    this.children[this.children.indexOf(old)] = child;
    child.parentElement = this;
    old.parentElement = null;

    return old;
  }

  removeChild(child) {
    this.children = this.children.filter(function (c) { return c !== child; });
    child.parentElement = null;

    return child;
  }

  remove() {
    if (this.parentElement) this.parentElement.removeChild(this);
  }

  contains(other) {
    for (var element = other; element; element = element.parentElement) {
      if (element === this) return true;
    }

    return false;
  }

  matches(selector) {
    var element = this;

    return selectorSteps(selector).every(function (step) { return matchesStep(element, step); });
  }

  closest(selector) {
    for (var element = this; element; element = element.parentElement) {
      if (element.matches(selector)) return element;
    }

    return null;
  }

  querySelectorAll(selector) {
    searches++;

    return descendantsOf([this]).filter(function (element) { return element.matches(selector); });
  }

  querySelector(selector) {
    return this.querySelectorAll(selector)[0] || null;
  }
};

// How many times anything searched a page (querySelector, querySelectorAll, getElementById), the helpers below
// included, so a test reads it just before and after the call it is about.
var searches = 0;

// Compound selectors of a tag, classes, ids and attributes ("iframe", ".pm-hud-btn", "[data-act]"); the script uses
// nothing else.
function selectorSteps(selector) {
  var steps = selector.match(/^[a-z]+|\.[\w-]+|#[\w-]+|\[[\w-]+\]/g) || [];

  if (steps.join('') !== selector) throw new Error('the fake page does not know the selector "' + selector + '"');

  return steps;
}

function matchesStep(element, step) {
  switch (step.charAt(0)) {
    case '.':
      return element.classList.contains(step.slice(1));
    case '#':
      return element.id === step.slice(1);
    case '[':
      return element.hasAttribute(step.slice(1, -1));
    default:
      return element.tagName === step;
  }
}

function descendantsOf(roots) {
  var found = [];

  roots.forEach(function (root) {
    root.children.forEach(function walk(child) {
      found.push(child);
      child.children.forEach(walk);
    });
  });

  return found;
}

var FakeDocument = class {
  constructor() {
    this.head = new FakeElement('head');
    this.body = new FakeElement('body');
    this.head.isRoot = true;
    this.body.isRoot = true;
  }

  createElement(tagName) { return new FakeElement(tagName); }

  createElementNS(namespace, tagName) { return new FakeElement(tagName); }

  createTextNode(text) {
    var node = new FakeElement('#text');

    node.textContent = text;

    return node;
  }

  createDocumentFragment() { return new FakeElement('#fragment'); }

  getElementById(id) {
    searches++;

    return descendantsOf([this.head, this.body]).filter(function (element) { return element.id === id; })[0] || null;
  }

  querySelectorAll(selector) {
    searches++;

    return descendantsOf([this.head, this.body]).filter(function (element) { return element.matches(selector); });
  }

  querySelector(selector) {
    return this.querySelectorAll(selector)[0] || null;
  }
};

// Root.html's own document, which holds the page iframes.
var document = new FakeDocument();

// What the script posts to the plugin.
var posted = [];

window.vuplex = {
  postMessage: function (message) { posted.push(message); }
};

// The in-game HUD: a CoreUI1 iframe whose tool column already holds the game's bag button.
function openHud() {
  var frame = document.body.appendChild(new FakeElement('iframe'));
  var column = new FakeElement('div');

  frame.src = 'https://game.local/UI/CoreUI1/CoreUI1.html';
  frame.contentDocument = new FakeDocument();
  column.className = 'vertical-tools';
  column.appendChild(new FakeElement('button'));
  frame.contentDocument.body.appendChild(column);

  return frame;
}

function hudButton() {
  var frame = document.querySelectorAll('iframe').filter(function (node) { return /CoreUI1/.test(node.src); })[0];

  return frame.contentDocument.querySelector('.pm-hud-btn');
}

function panelButton(action) {
  return document.querySelectorAll('[data-act]').filter(function (node) { return node.getAttribute('data-act') === action; })[0];
}

// The first panel element that does this action with these arguments.
function panelButtonWith(action, args) {
  return document.querySelectorAll('[data-act]').filter(function (node) { return node.getAttribute('data-act') === action && node.getAttribute('data-args') === args; })[0];
}

// Everything a node and the nodes inside it show, as one string, so two drawings can be compared whole.
function drawingOf(node) {
  return node.tagName + '#' + node.id + '.' + node.className + JSON.stringify(node.attrs) + JSON.stringify(node.style) + (node.src || '') + (node.disabled ? '!' : '') +
    '[' + node.text + '](' + node.children.map(drawingOf).join(',') + ')';
}

// A click as the game's click-through delivers one: dispatched on whatever is under the pointer, greyed buttons
// included, bubbling up until a listener stops it (webui-core.js:33-36).
function click(target) {
  var isStopped = false;
  var event = { target: target, stopPropagation: function () { isStopped = true; } };

  for (var node = target; node && !isStopped; node = node.parentElement) {
    (node.listeners.click || []).slice().forEach(function (listener) { listener(event); });
  }
}

// The game's click-through tells the game only where these are, skipping disabled ones (webui-core.js:126, 140-145); a
// click anywhere else on a page goes on to the world behind it. An element is covered when it or something around it
// is registered.
var REGISTERED = ['.unity-interactive', 'button', 'input', 'select', 'a', '[data-interactive]'];

function isCoveredForClicks(element) {
  for (var node = element; node; node = node.parentElement) {
    if (!node.disabled && REGISTERED.some(function (selector) { return node.matches(selector); })) return true;
  }

  return false;
}
