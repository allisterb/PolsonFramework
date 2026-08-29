const teak = await Assets.material('matte polished golden teak nautical decking planks, fine natural grain, warm amber finish');
console.log('Teak success: ' + teak.success);
if (!teak.success) {
    console.log('Failure: ' + teak.failureName);
    console.log('Remedy: ' + teak.remedy);
} else {
    Session.teakUri = teak.toDataUri();
    console.log('Teak size: ' + teak.size + 'px, wraps: ' + teak.tiling.wraps);
    console.log('Budget remaining: ' + Assets.budget.remaining);
}
