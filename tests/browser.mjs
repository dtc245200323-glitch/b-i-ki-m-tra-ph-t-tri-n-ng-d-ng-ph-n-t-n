import {chromium} from 'playwright';
import {mkdir,writeFile} from 'node:fs/promises';
import assert from 'node:assert/strict';
await mkdir('output/evidence',{recursive:true});
const browser=await chromium.launch({channel:'chrome',headless:true});
const errors=[];const results=[];
async function context(){const ctx=await browser.newContext({viewport:{width:1440,height:1000}});const page=await ctx.newPage();page.on('pageerror',e=>errors.push(e.message));return {ctx,page};}
async function shot(page,name){await page.screenshot({path:`output/evidence/${name}.png`,fullPage:true});}
async function login(page,role){await page.goto('http://localhost:5080');await page.locator('#email').fill(role+'@supportly.local');await page.locator('#password').fill('Supportly123!');await page.locator('#auth-form button[type=submit]').click();await page.locator('.sidebar').waitFor();await page.locator('#connection').filter({hasText:'Đã kết nối'}).waitFor();}
try{
const c=await context(),a=await context(),admin=await context();
await c.page.goto('http://localhost:5080');await shot(c.page,'01-dang-nhap');await c.page.locator('#switch-auth').click();await shot(c.page,'02-dang-ky');await c.page.locator('#switch-auth').click();
await login(c.page,'customer');await shot(c.page,'03-tong-quan');results.push('Đăng nhập khách hàng và xem tổng quan');
await c.page.locator('[data-page=chat]').click();await c.page.locator('#new-conversation').click();await c.page.locator('#subject').fill('Tư vấn nâng cấp gói dịch vụ');await c.page.locator('#modal-form [type=submit]').click();await c.page.locator('#message').waitFor();await c.page.locator('#message').fill('Chào bạn, mình muốn nâng cấp gói dịch vụ Premium.');await c.page.locator('#send-form button').click();await c.page.getByText('Chào bạn, mình muốn nâng cấp gói dịch vụ Premium.',{exact:false}).waitFor();
await login(a.page,'agent');await a.page.locator('[data-page=chat]').click();await a.page.locator('.conversation').filter({hasText:'Tư vấn nâng cấp gói dịch vụ'}).first().click();await a.page.locator('#accept').click();await a.page.locator('#message').waitFor();await a.page.locator('#message').fill('Chào bạn! Bạn muốn nâng cấp theo tháng hay theo năm?');await a.page.locator('#send-form button').click();await c.page.getByText('Chào bạn! Bạn muốn nâng cấp theo tháng hay theo năm?',{exact:false}).waitFor();await shot(c.page,'04-khach-hang-chat');await shot(a.page,'05-tu-van-vien');results.push('Tạo phiên, nhận phiên và nhắn tin hai chiều qua giao diện');
await login(admin.page,'admin');await admin.page.locator('[data-page=users]').click();await admin.page.locator('#users-table table').waitFor();await shot(admin.page,'06-quan-tri-vien');results.push('Quản trị viên xem danh sách người dùng');
await a.page.locator('#close-conversation').click();await a.page.locator('#modal-form [type=submit]').click();await c.page.getByText('Phiên đã kết thúc. Nội dung vẫn được lưu để tra cứu.').waitFor();results.push('Đóng phiên và chuyển sang chỉ đọc');
await c.page.locator('[data-page=history]').click();await c.page.locator('.conversation').filter({hasText:'Tư vấn nâng cấp gói dịch vụ'}).first().click();await c.page.locator('#messages .message').first().waitFor();await shot(c.page,'07-lich-su');results.push('Xem lịch sử đã được Worker lưu');
await c.page.setViewportSize({width:390,height:844});await c.page.locator('[data-page=dashboard]').click();await shot(c.page,'08-mobile');assert.equal(await c.page.evaluate(()=>document.documentElement.scrollWidth>window.innerWidth),false);results.push('Giao diện 390 px không tràn ngang');
assert.deepEqual(errors,[]);results.push('Không có lỗi JavaScript');console.log(results.join('\n'));
await writeFile('output/evidence/browser-results.json',JSON.stringify({executedAt:new Date().toISOString(),results,errors},null,2));
}finally{await browser.close();}
