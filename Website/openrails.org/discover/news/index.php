<?php include "../../shared/head.php" ?>
    <link rel="stylesheet" href="../../shared/iframe/iframe.css" type="text/css" />
  </head>

  <body>
    <div class="container"><!-- Centres content and sets fixed width to suit device -->
<?php include "../../shared/banners/choose_banner.php" ?>
<?php include "../../shared/banners/show_banner.php" ?>
<?php include "../../shared/menu.php" ?>
		<div class="row">
			<div class="col-md-12">
        <h1>Discover > News</h1>
      </div>
    </div>
		<div class="row">
			<div class="col-md-1"></div>
			<div class="col-md-5">
          <h2>Jan 2017 - Version 1.2</h2>
          <p>
            <a href="/discover/version-1-2/">Open Rails 1.2</a> released! <a href="/download/program/">Download it here</a>.
          </p>
          <hr />
          <h2>Mar 2016 - Version 1.1</h2>
          <p>
            <a href="/discover/version-1-1/">Open Rails 1.1</a> released!
          </p>
          <hr />
          <h2>Dec 2015 - More Access to Elvas Tower</h2>
          <p>
            The Elvas Tower forum plays a major role in developing Open Rails but has been closed to non-members following a dispute.
            We can now report that some of the <a href="http://www.elvastower.com/forums/">Open Rails sub-forums</a>
            are open again.
          </p>
          <hr />
          <h2>Jun 2015 - Great Zig Zag Railway</h2>
          <p>
            Peter Newell has just released (June 2105) the <a href="http://www.zigzag.coalstonewcastle.com.au/">Great Zig Zag Railway</a>,
            a steam route for Open Rails v1.0 (this 120MB download requires no other files).
          </p>
          <hr />
          <h2>May 2015 - Version 1.0</h2>
          <p>
            <a href="/discover/version-1-0/">Open Rails 1.0</a> released! <a href="/download/program/">Download it here</a>.
          </p>
          <hr />
          <h2>Apr 2015 - Demo Model 1</h2>
          <p>
          Open Rails first demonstration route <a href="/download/content">Demo Model 1</a> has been published.
          </p>
          <hr />
          <h2>Dec 2014 - 3D Cabs</h2>
          <p>
          <a href="http://www.dekosoft.com">Dekosoft Trains</a> has added locos exclusively for Open Rails to its range.
          These are GP30 diesels taking advantage of our 3D cab feature.
          </p>
          <hr />
          <h2>Jul 2014 - Web Site</h2>
          <p>
          The legacy graphics-heavy web site has been replaced by one based on Bootstrap which is both easier to maintain and
          suitable for phones and tablets as well as PCs.
          </p><p>
          You can still <a href="/web1/index.html">see an archive of the old site</a>.
          </p>
          <hr />
          <h2>Apr 2014 - Installer</h2>
          <p>
          An installer is now available, so Open Rails and its pre-requisites such as XNA can be delivered in a single download.
          </p>
          <hr />
          <h2>Apr 2014 - Smoother, More Detailed Graphics</h2>
          <p>
            Open Rails currently uses DirectX 9 and, although this is not the latest version of DirectX, hidden away inside is a method for reducing the
            number of "draw calls" which the CPU makes to the GPU. Fewer calls mean higher frame rates, smoother motion and the capacity to handle
            more detail.
          </p><p>
          The technique is called "hardware instancing" and allows identical objects (e.g. trees in a forest) to be combined into a single draw
          call. The work is transferred to the GPU which copies them as many times as necessary and usually has spare capacity.
          </p><p>
          You can expect some increases in frame rate, especially on routes with many identical objects. To turn this on, tick the checkbox for<br>
          <span class="tt">Options > Experimental > Use model instancing</span>
          </p>
          <hr />
          <h2>Mar 2014 - Additional Languages</h2>
          <p>
          Open Rails becomes available in additional languages, initially eight including Chinese.<br />
          </p><p>
            <img src="additional_languages.jpg" height=415 width=314 alt='screendump listing languages'/>
          </p>
          <hr />
          <h2>Mar 2014 - Work Starts On Timetables</h2>
          <p>
            A schedule of trains (or timetable) is nearly impossible to arrange in Microsoft Train Simulator as AI trains don't adhere to booked station stops. In Open Rails,
            the situation is better but an activity with a player train and AI traffic is still very different from a timetable.
          </p><p>
            Work has now begun on a timetable element which is an alternative to the usual activity. The timetable will contain all the details
            needed for each scheduled train - path, consist, booked stops etc.. Conventional activities will continue as before.
          </p><p>
            Also there is no longer any distinction between player train or AI train - any train in the timetable can be selected as the player
            train, the others are operated by a remote player or by the simulator.
          </p>
          <hr />
          <h2>Feb 2014</h2>
          <p>
          Fog is developed from just softening the horizon into a realistic effect which users can fully control.
          </p>
			</div>
			<div class="col-md-1"></div>
			<div class="col-md-4">
        <h2>Recent Code Changes</h2>
		<ul>
			<?php include "../../api/update/testing/changelog.html" ?>
		</ul>
		<p><a href='../../download/changes/'>See more code changes</a></p>
  			<div class="col-md-1">
        </div>
      </div>
		</div>
<?php include "../../shared/tail.php" ?>
<?php include "../../shared/banners/preload_next_banner.php" ?>
  </body>
</html>

