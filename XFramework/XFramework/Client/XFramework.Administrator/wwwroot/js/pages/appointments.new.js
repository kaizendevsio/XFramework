/**
 *
 * Appointments New
 *
 * Appointments New page content scripts. Initialized from scripts.js file.
 *
 *
 */

class NewAppointment {
  constructor() {
    this._initWizard();
    this._initBarrating();
    this._initDatepicker();
    this._initTimepicker();
  }

  // Appointments wizard initialization
  _initWizard() {
    if (document.getElementById('appointmentsWizard') !== null) {
      new Wizard(document.getElementById('appointmentsWizard'));
    }
  }

  // Doctor rating initialization
  _initBarrating() {
    if (jQuery().barrating) {
      jQuery('.rating').each(function () {
        const current = jQuery(this).data('initialRating');
        const readonly = jQuery(this).data('readonly');
        const showSelectedRating = jQuery(this).data('showSelectedRating');
        const showValues = jQuery(this).data('showValues');
        jQuery(this).barrating({
          initialRating: current,
          readonly: readonly,
          showValues: showValues,
          showSelectedRating: showSelectedRating,
          onSelect: function (value, text) {},
          onClear: function (value, text) {},
        });
      });
    }
  }

  // Appointments date picker initialization
  _initDatepicker() {
    jQuery('#datePickerEmbed').datepicker();
  }

  // Appointments time picker initialization
  _initTimepicker() {
    jQuery('#selectTopLabel').select2({minimumResultsForSearch: Infinity, placeholder: ''});
  }
}
