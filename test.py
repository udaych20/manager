ngAfterViewChecked(): void {
  this.setPaginatorAriaLabels();
}

private setPaginatorAriaLabels(): void {
  const root = this.elementRef.nativeElement as HTMLElement;

  const paginatorButtons = [
    { selector: '.p-paginator-first', label: 'First page' },
    { selector: '.p-paginator-prev', label: 'Previous page' },
    { selector: '.p-paginator-next', label: 'Next page' },
    { selector: '.p-paginator-last', label: 'Last page' }
  ];

  paginatorButtons.forEach(item => {
    root.querySelectorAll(item.selector).forEach((button: Element) => {
      if (!button.getAttribute('aria-label')) {
        this.renderer.setAttribute(button, 'aria-label', item.label);
      }

      if (!button.getAttribute('title')) {
        this.renderer.setAttribute(button, 'title', item.label);
      }
    });
  });
}
