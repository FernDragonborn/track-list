<script>
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';

	let { data } = $props();

	$effect(() => {
		if (data.user?.username) {
			goto(resolve(`/profile/${encodeURIComponent(data.user.username)}`), { replaceState: true });
		}
	});
</script>
