/**
 *
 * Consult
 *
 * Consult page content scripts. Initialized from scripts.js file.
 *
 *
 */

class Consult {
  constructor() {
    this._initQuill();
  }

  // Consult text editor initialization
  _initQuill() {
    if (typeof Quill !== 'undefined' && typeof Active !== 'undefined') {
      Quill.register('modules/active', Active);
      const editorModules = {
        toolbar: [
          ['bold', 'italic', 'underline', 'strike'],
          [{header: [1, 2, 3, 4, 5, 6, false]}],
          [{list: 'ordered'}, {list: 'bullet'}],
          [{size: ['small', false, 'large', 'huge']}],
          [{color: []}],
          [{align: []}],
        ],
        active: {},
      };
      if (document.getElementById('quillEditorFilledEmail')) {
        new Quill('#quillEditorFilledEmail', {
          modules: editorModules,
          theme: 'bubble',
          placeholder: 'Answer',
        });
      }
    }
  }
}
