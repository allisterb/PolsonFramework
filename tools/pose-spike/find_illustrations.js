// Looking for lead images that are DRAWN full figures rather than photographs, to test the case
// that actually matters. Resolution spends nothing, so the list can be long.
const candidates = [
    'Tintin', 'Asterix', 'Little Nemo', 'The Yellow Kid', 'Popeye', 'Astro Boy',
    'The Blue Boy', 'Pinkie (painting)', 'Gibson Girl', 'Uncle Sam',
    'Nude Descending a Staircase, No. 2', 'Ukiyo-e', 'Fashion plate'
];
const rows = [];
for (const c of candidates) {
    const m = await Photo.resolve(c);
    rows.push({
        query: c.slice(0, 22),
        ok: m.success ? 'y' : (m.failureName + ''),
        aspect: m.sourceWidth && m.sourceHeight ? +(m.sourceWidth / m.sourceHeight).toFixed(2) : '-',
        licence: (m.licence && m.licence.name ? m.licence.name : '-').slice(0, 14),
        file: (m.file || '').slice(0, 40)
    });
}
table(rows);
log(`photo budget: ${Photo.budget.remaining} of ${Photo.budget.total} remaining`);
exit('resolved only - nothing fetched, nothing spent');
