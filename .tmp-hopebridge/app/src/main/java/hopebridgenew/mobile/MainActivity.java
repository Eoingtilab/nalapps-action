package hopebridgenew.mobile;

import android.app.Activity;
import android.app.DownloadManager;
import android.content.ActivityNotFoundException;
import android.content.Context;
import android.content.Intent;
import android.graphics.Color;
import android.net.Uri;
import android.os.Bundle;
import android.os.Environment;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.webkit.CookieManager;
import android.webkit.SslErrorHandler;
import android.webkit.URLUtil;
import android.webkit.ValueCallback;
import android.webkit.WebChromeClient;
import android.webkit.WebResourceError;
import android.webkit.WebResourceRequest;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.FrameLayout;
import android.widget.ProgressBar;
import android.widget.Toast;

import java.net.URISyntaxException;
import java.util.Locale;

public class MainActivity extends Activity {
	private static final String START_URL = "https://hope-bridge.kr/";
	private static final int FILE_CHOOSER_REQUEST = 1001;

	private WebView webView;
	private ProgressBar progressBar;
	private ValueCallback<Uri[]> filePathCallback;

	@Override
	protected void onCreate(Bundle savedInstanceState) {
		super.onCreate(savedInstanceState);
		getWindow().setStatusBarColor(Color.WHITE);
		getWindow().setNavigationBarColor(Color.WHITE);
		buildUi();
		configureWebView();
		if (savedInstanceState == null || webView.restoreState(savedInstanceState) == null) {
			webView.loadUrl(START_URL);
		}
	}

	private void buildUi() {
		FrameLayout root = new FrameLayout(this);
		root.setBackgroundColor(Color.WHITE);

		webView = new WebView(this);
		root.addView(webView, new FrameLayout.LayoutParams(
			ViewGroup.LayoutParams.MATCH_PARENT,
			ViewGroup.LayoutParams.MATCH_PARENT
		));

		progressBar = new ProgressBar(this, null, android.R.attr.progressBarStyleHorizontal);
		progressBar.setMax(100);
		FrameLayout.LayoutParams progressParams = new FrameLayout.LayoutParams(
			ViewGroup.LayoutParams.MATCH_PARENT,
			Math.max(3, Math.round(3 * getResources().getDisplayMetrics().density))
		);
		progressParams.gravity = Gravity.TOP;
		root.addView(progressBar, progressParams);
		setContentView(root);
	}

	private void configureWebView() {
		WebSettings settings = webView.getSettings();
		settings.setJavaScriptEnabled(true);
		settings.setDomStorageEnabled(true);
		settings.setDatabaseEnabled(true);
		settings.setAllowFileAccess(false);
		settings.setAllowContentAccess(true);
		settings.setLoadsImagesAutomatically(true);
		settings.setLoadWithOverviewMode(true);
		settings.setUseWideViewPort(true);
		settings.setBuiltInZoomControls(false);
		settings.setDisplayZoomControls(false);
		settings.setSupportMultipleWindows(true);
		settings.setJavaScriptCanOpenWindowsAutomatically(true);
		settings.setMediaPlaybackRequiresUserGesture(false);
		settings.setMixedContentMode(WebSettings.MIXED_CONTENT_NEVER_ALLOW);
		settings.setUserAgentString(settings.getUserAgentString() + " HopeBridgeAndroid/2.0");

		CookieManager cookies = CookieManager.getInstance();
		cookies.setAcceptCookie(true);
		cookies.setAcceptThirdPartyCookies(webView, true);

		webView.setWebViewClient(new WebViewClient() {
			@Override
			public boolean shouldOverrideUrlLoading(WebView view, WebResourceRequest request) {
				return handleUri(request.getUrl());
			}

			@Override
			@SuppressWarnings("deprecation")
			public boolean shouldOverrideUrlLoading(WebView view, String url) {
				return handleUri(Uri.parse(url));
			}

			@Override
			public void onPageFinished(WebView view, String url) {
				super.onPageFinished(view, url);
				progressBar.setVisibility(View.GONE);
				CookieManager.getInstance().flush();
			}

			@Override
			public void onReceivedError(WebView view, WebResourceRequest request, WebResourceError error) {
				super.onReceivedError(view, request, error);
				if (request.isForMainFrame()) {
					Toast.makeText(MainActivity.this, "페이지를 불러오지 못했습니다. 인터넷 연결을 확인해 주세요.", Toast.LENGTH_LONG).show();
				}
			}

			@Override
			public void onReceivedSslError(WebView view, SslErrorHandler handler, android.net.http.SslError error) {
				handler.cancel();
				Toast.makeText(MainActivity.this, "보안 연결을 확인할 수 없습니다.", Toast.LENGTH_LONG).show();
			}
		});

		webView.setWebChromeClient(new WebChromeClient() {
			@Override
			public void onProgressChanged(WebView view, int newProgress) {
				progressBar.setProgress(newProgress);
				progressBar.setVisibility(newProgress >= 100 ? View.GONE : View.VISIBLE);
			}

			@Override
			public boolean onShowFileChooser(WebView webView, ValueCallback<Uri[]> callback, FileChooserParams params) {
				if (filePathCallback != null) {
					filePathCallback.onReceiveValue(null);
				}
				filePathCallback = callback;
				Intent intent;
				try {
					intent = params.createIntent();
				} catch (Exception e) {
					intent = new Intent(Intent.ACTION_OPEN_DOCUMENT);
					intent.addCategory(Intent.CATEGORY_OPENABLE);
					intent.setType("*/*");
				}
				try {
					startActivityForResult(intent, FILE_CHOOSER_REQUEST);
					return true;
				} catch (ActivityNotFoundException e) {
					filePathCallback = null;
					Toast.makeText(MainActivity.this, "파일 선택 앱을 찾을 수 없습니다.", Toast.LENGTH_LONG).show();
					return false;
				}
			}

			@Override
			public boolean onCreateWindow(WebView view, boolean isDialog, boolean isUserGesture, android.os.Message resultMsg) {
				WebView child = new WebView(MainActivity.this);
				child.getSettings().setJavaScriptEnabled(true);
				child.getSettings().setDomStorageEnabled(true);
				child.setWebViewClient(new WebViewClient() {
					@Override
					public boolean shouldOverrideUrlLoading(WebView v, WebResourceRequest request) {
						Uri uri = request.getUrl();
						if (uri != null && isHttp(uri)) {
							webView.loadUrl(uri.toString());
							return true;
						}
						return handleUri(uri);
					}
				});
				WebView.WebViewTransport transport = (WebView.WebViewTransport) resultMsg.obj;
				transport.setWebView(child);
				resultMsg.sendToTarget();
				return true;
			}
		});

		webView.setDownloadListener((url, userAgent, contentDisposition, mimeType, contentLength) -> {
			try {
				DownloadManager.Request request = new DownloadManager.Request(Uri.parse(url));
				request.setMimeType(mimeType);
				String cookie = CookieManager.getInstance().getCookie(url);
				if (cookie != null) request.addRequestHeader("Cookie", cookie);
				if (userAgent != null) request.addRequestHeader("User-Agent", userAgent);
				String filename = URLUtil.guessFileName(url, contentDisposition, mimeType);
				request.setTitle(filename);
				request.setNotificationVisibility(DownloadManager.Request.VISIBILITY_VISIBLE_NOTIFY_COMPLETED);
				request.setDestinationInExternalPublicDir(Environment.DIRECTORY_DOWNLOADS, filename);
				DownloadManager manager = (DownloadManager) getSystemService(Context.DOWNLOAD_SERVICE);
				manager.enqueue(request);
				Toast.makeText(MainActivity.this, "다운로드를 시작했습니다.", Toast.LENGTH_SHORT).show();
			} catch (Exception e) {
				openExternal(Uri.parse(url));
			}
		});
	}

	private boolean handleUri(Uri uri) {
		if (uri == null) return false;
		if (isHttp(uri)) return false;
		String scheme = uri.getScheme() == null ? "" : uri.getScheme().toLowerCase(Locale.ROOT);
		if ("intent".equals(scheme)) {
			try {
				Intent intent = Intent.parseUri(uri.toString(), Intent.URI_INTENT_SCHEME);
				startActivity(intent);
				return true;
			} catch (URISyntaxException | ActivityNotFoundException ignored) {
				return true;
			}
		}
		if ("tel".equals(scheme) || "mailto".equals(scheme) || "sms".equals(scheme) || "market".equals(scheme)) {
			openExternal(uri);
			return true;
		}
		return false;
	}

	private boolean isHttp(Uri uri) {
		String scheme = uri.getScheme();
		return "http".equalsIgnoreCase(scheme) || "https".equalsIgnoreCase(scheme);
	}

	private void openExternal(Uri uri) {
		try {
			startActivity(new Intent(Intent.ACTION_VIEW, uri));
		} catch (ActivityNotFoundException e) {
			Toast.makeText(this, "연결할 앱을 찾을 수 없습니다.", Toast.LENGTH_SHORT).show();
		}
	}

	@Override
	protected void onActivityResult(int requestCode, int resultCode, Intent data) {
		super.onActivityResult(requestCode, resultCode, data);
		if (requestCode != FILE_CHOOSER_REQUEST || filePathCallback == null) return;
		Uri[] result = WebChromeClient.FileChooserParams.parseResult(resultCode, data);
		filePathCallback.onReceiveValue(result);
		filePathCallback = null;
	}

	@Override
	public void onBackPressed() {
		if (webView != null && webView.canGoBack()) {
			webView.goBack();
		} else {
			super.onBackPressed();
		}
	}

	@Override
	protected void onSaveInstanceState(Bundle outState) {
		if (webView != null) webView.saveState(outState);
		super.onSaveInstanceState(outState);
	}

	@Override
	protected void onDestroy() {
		if (filePathCallback != null) {
			filePathCallback.onReceiveValue(null);
			filePathCallback = null;
		}
		if (webView != null) {
			webView.stopLoading();
			webView.loadUrl("about:blank");
			webView.clearHistory();
			webView.removeAllViews();
			webView.destroy();
			webView = null;
		}
		super.onDestroy();
	}
}
