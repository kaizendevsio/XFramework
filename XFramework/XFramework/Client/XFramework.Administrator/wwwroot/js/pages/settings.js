/**
 *
 * Settings
 *
 * Settings page content scripts. Initialized from scripts.js file.
 *
 *
 */

class ProfileSettings {
  constructor() {
    this._initGenderSelect();
    this._initBirthdayDatePicker();
  }

  // Gender select2
  _initGenderSelect() {
    if (document.getElementById('genderSelect') !== null && jQuery().select2) {
      jQuery('#genderSelect').select2({minimumResultsForSearch: Infinity, placeholder: ''});
    }
  }

  // Birthday date picker
  _initBirthdayDatePicker() {
    if (document.getElementById('birthday') !== null && jQuery().datepicker) {
      jQuery('#birthday').datepicker({
        autoclose: true,
      });
    }
  }
}
