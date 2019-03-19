﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FDK
{
    public partial class AppForm : Form
    {
        // 起動、終了

        public AppForm( 進行描画 work )
        {
            InitializeComponent();

            this.進行描画 = work;
        }

        public virtual void 開始する()
        {
            using( Log.Block( FDKUtilities.現在のメソッド名 ) )
            {
                this._未初期化 = false;
                TimeGetTime.timeBeginPeriod( 1 );
                PowerManagement.システムの自動スリープと画面の自動非表示を抑制する();

                this.Activate();    // ウィンドウが後ろに隠れることがあるので、最前面での表示を保証する。

                this.進行描画.開始する( this.ClientSize, new Size( 1920, 1080 ), this.Handle );
            }
        }

        public virtual void 終了する()
        {
            using( Log.Block( FDKUtilities.現在のメソッド名 ) )
            {
                this.進行描画.終了する().WaitOne();  // 終了するまで待つ

                PowerManagement.システムの自動スリープと画面の自動非表示の抑制を解除する();
                TimeGetTime.timeEndPeriod( 1 );

                this._未初期化 = true;
            }
        }


        protected override void OnLoad( EventArgs e )
        {
            using( Log.Block( FDKUtilities.現在のメソッド名 ) )
            {
                this.開始する();
                base.OnLoad( e );
            }
        }

        protected override void OnClosing( CancelEventArgs e )
        {
            using( Log.Block( FDKUtilities.現在のメソッド名 ) )
            {
                this.終了する();
                base.OnClosing( e );
            }
        }


        protected 進行描画 進行描画;

        /// <summary>
        ///     起動直後は true, OnLoad されて false, OnClosing で true。
        /// </summary>
        private bool _未初期化 = true;

        /// <summary>
        ///     このフォームと進行描画タスク間での排他用。
        /// </summary>
        protected readonly object 進行描画排他ロック = new object();

        /// <summary>
        ///		フォーム生成時のパラメータを編集して返す。
        /// </summary>
        protected override CreateParams CreateParams
        {
            get
            {
                // DWM によってトップウィンドウごとに割り当てられるリダイレクトサーフェスを持たない。（リダイレクトへの画像転送がなくなる分、少し速くなるらしい）
                const int WS_EX_NOREDIRECTIONBITMAP = 0x00200000;

                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_NOREDIRECTIONBITMAP;
                return cp;
            }
        }



        // Raw Input 

        private const int WM_INPUT = 0x00FF;

        /// <summary>
        ///     WM_INPUT ハンドラ。GUIスレッドで実行される。 
        /// </summary>
        /// <param name="msg">WM_INPUT のメッセージ。</param>
        protected virtual void OnInput( in System.Windows.Forms.Message msg )
        {
            // 派生クラスで実装する。
        }

        /// <summary>
        ///     このフォームのウィンドウメッセージ処理。
        /// </summary>
        protected override void WndProc( ref System.Windows.Forms.Message msg )
        {
            switch( msg.Msg )
            {
                case WM_INPUT:
                    this.OnInput( msg );
                    break;
            }

            base.WndProc( ref msg );
        }


        // フォームのサイズ変更

        /// <summary>
        ///     ユーザによるフォームのサイズ変更が完了した。
        /// </summary>
        /// <remarks>
        ///     1. GUIフレッドで、フォームのサイズが確定されるとこのハンドラが呼び出される。
        ///     2. 進行描画スレッドで、フォームの新しいサイズに合わせたグラフィックリソースの再構築を行う。
        /// </remarks>
        protected override void OnResizeEnd( EventArgs e )
        {
            if( this.WindowState == FormWindowState.Minimized )
            {
                // (A) 最小化された → 何もしない
            }
            else if( this.ClientSize.IsEmpty )
            {
                // (B) クライアントサイズが空 → たまに起きるらしい。スキップする。
            }
            else if( this._未初期化 )
            {
                // (C) メインループが始まる前にも数回呼び出されることがある → スキップする。
            }
            else
            {
                // (D) スワップチェーンとその依存リソースを解放し、改めて作成しなおす。
                this.進行描画.サイズを変更する( this.ClientSize ).WaitOne();   // 完了するまで待つ
            }

            base.OnResizeEnd( e );
        }


        // 画面モードの変更

        public 画面モード 画面モード
        {
            get => this._画面モード;
            set => this._画面モードを変更する( value );
        }

        private void _画面モードを変更する( 画面モード 新モード )
        {
            switch( 新モード )
            {
                case 画面モード.ウィンドウ:

                    if( this._画面モード != 画面モード.ウィンドウ )
                    {
                        this._ウィンドウモードの情報のバックアップ.clientSize = this.ClientSize;
                        this._ウィンドウモードの情報のバックアップ.formBorderStyle = this.FormBorderStyle;

                        // (参考) http://www.atmarkit.co.jp/ait/articles/0408/27/news105.html
                        this.WindowState = FormWindowState.Normal;
                        this.FormBorderStyle = FormBorderStyle.None;
                        this.WindowState = FormWindowState.Maximized;

                        Cursor.Hide();

                        this._画面モード = 画面モード.ウィンドウ;
                    }
                    break;

                case 画面モード.全画面:

                    if( this._画面モード != 画面モード.全画面 )
                    {
                        // 正確には、「全画面(fullscreen)」ではなく「最大化(maximize)」。
                        this.WindowState = FormWindowState.Normal;
                        this.ClientSize = this._ウィンドウモードの情報のバックアップ.clientSize;
                        this.FormBorderStyle = this._ウィンドウモードの情報のバックアップ.formBorderStyle;

                        Cursor.Show();

                        this._画面モード = 画面モード.全画面;
                    }
                    break;
            }
        }

        private 画面モード _画面モード = 画面モード.ウィンドウ;

        /// <summary>
        ///		ウィンドウを全画面モードにする直前に取得し、
        ///		再びウィンドウモードに戻して状態を復元する時に参照する。
        /// </summary>
        private (Size clientSize, FormBorderStyle formBorderStyle) _ウィンドウモードの情報のバックアップ;
    }
}
