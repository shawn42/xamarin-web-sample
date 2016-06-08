// import Ember from 'ember';

var xamarinCallbacks = window.xamarinCallbacks = {
  i: 0,
  callbacks: {},
};

var invokeCallback = xamarinCallbacks.invokeCallback = function(name, argsArray, callback) {
  var cbn = "" + name + window.xamarinCallbacks.i;
  xamarinCallbacks.callbacks[cbn] = callback;
  xamarinCallbacks.i += 1;
  window.Native("Invoke", {
    FuncName: name,
    Payload: JSON.stringify(argsArray),
    Callback: cbn
  });
};


var Subscription = Ember.Object.extend({
  callbackName: null,

  destroy: function () {
    var callbackName = this.get("callbackName");

    window.Native("Unsubscribe", {
      Callback: callbackName,
    });
    xamarinCallbacks.destroyCallback(callbackName);

    this._super();
  },
});

var subscribeEvent = xamarinCallbacks.subscribeEvent = function(name, argsArray, callback) {
  var callbackName = "" + name + window.xamarinCallbacks.i;
  xamarinCallbacks.callbacks[callbackName] = callback;
  xamarinCallbacks.i += 1;

  window.Native("Subscribe", {
    FuncName: name,
    Payload: JSON.stringify(argsArray),
    Callback: callbackName
  });

  return Subscription.create({
    callbackName: callbackName,
  });
};

var returnValues = xamarinCallbacks.returnValues = function(cbn, err, argStrings) {
  var cbs = window.xamarinCallbacks.callbacks;
  var cb = cbs[cbn];
  delete cbs[cbn];

  if (!cb) {
    return;
  }

  var args = (argStrings || []).map(function(s) { return JSON.parse(s); });
  args.unshift(err);
  Ember.run(function() {
    cb.apply(window, args);
  });
};

xamarinCallbacks.eventOccurred = function(cbn, argString) {
  var cbs = window.xamarinCallbacks.callbacks;
  var cb = cbs[cbn];
  if (!cb) {
    return;
  }

  var arg = JSON.parse(argString);
  Ember.run(function() {
    cb.call(window, arg);
  });
};

xamarinCallbacks.destroyCallback = function(cbn) {
  delete window.xamarinCallbacks.callbacks[cbn];
};

var invokePromise = xamarinCallbacks.invokePromise = function (name) {
  for (var _len = arguments.length, args = Array(_len > 1 ? _len - 1 : 0), _key = 1; _key < _len; _key++) {
    args[_key - 1] = arguments[_key];
  }

  return new Ember.RSVP.Promise(function (accept, reject) {
    invokeCallback(name, args, function (err, result) {
      if (err) {
        reject(err);
      } else {
        accept(result);
      }
    });
  });
};
var XamarinInterop = {
  call: invokePromise,
  subscribe: function subscribe(event) {
    for (var _len = arguments.length, args = Array(_len > 1 ? _len - 1 : 0), _key = 1; _key < _len; _key++) {
      args[_key - 1] = arguments[_key];
    }

    var cb = args.pop();
    return subscribeEvent(event, args, cb);
  }
};

//export { invokeCallback, returnValues, invokePromise };
//export default XamarinInterop;

window.invokeCallback = invokeCallback;
window.returnValues = returnValues;
window.invokePromise = invokePromise;
window.XamarinInterop = XamarinInterop;
