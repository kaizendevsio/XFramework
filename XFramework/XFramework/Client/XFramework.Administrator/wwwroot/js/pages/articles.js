/**
 *
 * Articles
 *
 * Articles page content scripts. Initialized from scripts.js file.
 *
 *
 */

class Articles {
  constructor() {
    this._initGlide();
  }

  // Categories glide initialization
  _initGlide() {
    if (document.querySelector('#glideCenter')) {
      new GlideCustom(
        document.querySelector('#glideCenter'),
        {
          gap: 0,
          type: 'carousel',
          perView: 6,
          peek: {before: 50, after: 50},
          breakpoints: {
            600: {perView: 1},
            1000: {perView: 2},
            1400: {perView: 3},
            1900: {perView: 5},
            3840: {perView: 6},
          },
        },
        true,
      ).mount();
    }
  }
}
