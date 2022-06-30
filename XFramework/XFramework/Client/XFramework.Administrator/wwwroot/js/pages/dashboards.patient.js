/**
 *
 * Dashboards Patient
 *
 * Dashboards Patient page content scripts. Initialized from scripts.js file.
 *
 *
 */

class DashboardsPatient {
  constructor() {
    // Initialization of the page plugins
    this._initStatsCarousel();
    this._initAppointmentsCarousel();
  }

  // Stats carousel initialization
  _initStatsCarousel() {
    if (document.querySelector('#statsCarousel') !== null && typeof GlideCustom !== 'undefined') {
      new GlideCustom(
        document.querySelector('#statsCarousel'),
        {
          gap: 0,
          rewind: false,
          bound: true,
          perView: 6,
          breakpoints: {
            400: {perView: 1},
            600: {perView: 2},
            1400: {perView: 4},
            1600: {perView: 4},
            1900: {perView: 5},
            3840: {perView: 6},
          },
        },
        true,
      ).mount();
    }
  }

  // Appointments carousel initialization
  _initAppointmentsCarousel() {
    if (document.querySelector('#appointmentsCarousel') !== null && typeof GlideCustom !== 'undefined') {
      new Glide(
        document.querySelector('#appointmentsCarousel'),
        {
          gap: 0,
          rewind: false,
          bound: true,
          perView: 7,
          breakpoints: {
            400: {perView: 4},
            600: {perView: 6},
            1200: {perView: 12},
            1400: {perView: 6},
            1600: {perView: 6},
            1900: {perView: 8},
            3840: {perView: 9},
          },
        },
        true,
      ).mount();
    }
  }
}
