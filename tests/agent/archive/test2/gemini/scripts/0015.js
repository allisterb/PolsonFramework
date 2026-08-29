console.log('Budget total: ' + Assets.budget.total);
console.log('Budget remaining: ' + Assets.budget.remaining);
console.log('Budget spent: ' + Assets.budget.spent);

const testClassify1 = Assets.classify('weathered teak wood decking');
const testClassify2 = Assets.classify('a romantic sailboat cruise ship');
console.log('Classify 1: ' + JSON.stringify(testClassify1));
console.log('Classify 2: ' + JSON.stringify(testClassify2));
