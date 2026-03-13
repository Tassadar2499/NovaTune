import { createApp } from 'vue';
import { createPinia } from 'pinia';
import { VueQueryPlugin } from '@tanstack/vue-query';
import App from './App.vue';
import router from './router';
import { useAuthStore } from '@/stores/auth';
import './styles/main.css';

const app = createApp(App);
const pinia = createPinia();

app.use(pinia);
app.use(VueQueryPlugin);

const auth = useAuthStore(pinia);
await auth.initializeSession();

app.use(router);

app.mount('#app');
