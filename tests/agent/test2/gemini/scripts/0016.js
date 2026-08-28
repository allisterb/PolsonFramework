const linen = await Assets.material('warm cream fine linen textured paper surface, subtle organic weave');
console.log('Linen success: ' + linen.success);
if (!linen.success) {
    console.log('Failure name: ' + linen.failureName);
    console.log('Remedy: ' + linen.remedy);
} else {
    Session.linenUri = linen.toDataUri();
    console.log('Linen size: ' + linen.size + 'px, wraps: ' + linen.tiling.wraps);
    console.log('Budget remaining: ' + Assets.budget.remaining);
}
